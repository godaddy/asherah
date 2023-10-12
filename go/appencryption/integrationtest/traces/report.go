package traces

import (
	"context"
	"fmt"
	"io"
	"math/rand"
	"time"

	"github.com/rcrowley/go-metrics"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/internal"
	"github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
	"github.com/godaddy/asherah/go/appencryption/pkg/kms"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence"
)

type Stats struct {
	RequestCount             uint64
	KMSOpCount               uint64
	KMSEncryptCount          uint64
	KMSDecryptCount          uint64
	MetastoreOpCount         uint64
	MetastoreLoadCount       uint64
	MetastoreLoadLatestCount uint64
	MetastoreStoreCount      uint64
	OpRate                   float64
}

type Reporter interface {
	Report(Stats, options)
}

type Provider interface {
	Provide(ctx context.Context, keys chan<- interface{})
}

type reporter struct {
	w             io.Writer
	headerPrinted bool
}

func NewReporter(w io.Writer) Reporter {
	return &reporter{w: w}
}

func (r *reporter) Report(st Stats, opt options) {
	if !r.headerPrinted {
		fmt.Fprintf(r.w, "Requests,KMSOps,KMSEncrypts,KMSDecrypts,MetastoreOps,MetastoreLoads,MetastoreLoadLatests,MetastoreStores,OpRate,CacheSize\n")
		r.headerPrinted = true
	}

	fmt.Fprintf(
		r.w,
		"%d,%d,%d,%d,%d,%d,%d,%d,%.04f,%d\n",
		st.RequestCount,
		st.KMSOpCount,
		st.KMSEncryptCount,
		st.KMSDecryptCount,
		st.MetastoreOpCount,
		st.MetastoreLoadCount,
		st.MetastoreLoadLatestCount,
		st.MetastoreStoreCount,
		st.OpRate,
		opt.cacheSize)
}

// trackedKMS is a KeyManagementService that tracks the number of encrypt and
// decrypt operations.
type trackedKMS struct {
	appencryption.KeyManagementService

	decryptCounter metrics.Counter
	encryptCounter metrics.Counter
}

func newTrackedKMS(kms appencryption.KeyManagementService) *trackedKMS {
	return &trackedKMS{
		KeyManagementService: kms,
		decryptCounter:       metrics.NewCounter(),
		encryptCounter:       metrics.NewCounter(),
	}
}

func (t *trackedKMS) DecryptKey(ctx context.Context, key []byte) ([]byte, error) {
	t.decryptCounter.Inc(1)
	return t.KeyManagementService.DecryptKey(ctx, key)
}

func (t *trackedKMS) EncryptKey(ctx context.Context, key []byte) ([]byte, error) {
	t.encryptCounter.Inc(1)
	return t.KeyManagementService.EncryptKey(ctx, key)
}

// delayedMetastore is a Metastore that delays all operations by a configurable
// amount of time.
type delayedMetastore struct {
	m      *persistence.MemoryMetastore
	delay  time.Duration
	jitter time.Duration

	loadCounter       metrics.Counter
	loadLatestCounter metrics.Counter
	storeCounter      metrics.Counter
}

func newDelayedMetastore(delay time.Duration, jitter time.Duration) *delayedMetastore {
	return &delayedMetastore{
		m:      persistence.NewMemoryMetastore(),
		delay:  delay,
		jitter: jitter,

		loadCounter:       metrics.NewCounter(),
		loadLatestCounter: metrics.NewCounter(),
		storeCounter:      metrics.NewCounter(),
	}
}

func (d *delayedMetastore) delayWithJitter() {
	ch := make(chan int)
	go func() {
		randJitter := int64(0)
		if d.jitter > 0 {
			randJitter = rand.Int63n(int64(d.jitter))
		}

		if d.delay > 0 {
			time.Sleep(d.delay + time.Duration(randJitter))
		}

		ch <- 1
	}()

	<-ch
}

func (d *delayedMetastore) Load(ctx context.Context, keyID string, created int64) (*appencryption.EnvelopeKeyRecord, error) {
	d.loadCounter.Inc(1)

	d.delayWithJitter()

	return d.m.Load(ctx, keyID, created)
}

func (d *delayedMetastore) LoadLatest(ctx context.Context, keyID string) (*appencryption.EnvelopeKeyRecord, error) {
	d.loadLatestCounter.Inc(1)

	d.delayWithJitter()

	return d.m.LoadLatest(ctx, keyID)
}

func (d *delayedMetastore) Store(ctx context.Context, keyID string, created int64, envelope *appencryption.EnvelopeKeyRecord) (bool, error) {
	d.storeCounter.Inc(1)

	d.delayWithJitter()

	return d.m.Store(ctx, keyID, created, envelope)
}

type options struct {
	policy         string
	cacheSize      int
	reportInterval int
	maxItems       int
}

var policies = []string{
	"session-legacy",
}

const (
	product          = "enclibrary"
	service          = "asherah"
	staticKey        = "thisIsAStaticMasterKeyForTesting"
	payloadSizeBytes = 100
)

var c = aead.NewAES256GCM()

//nolint:gocyclo
func benchmarkSessionFactory(p Provider, r Reporter, opt options) {
	static, err := kms.NewStatic(staticKey, c)
	if err != nil {
		panic(err)
	}

	km := newTrackedKMS(static)
	config := getConfig(opt)
	ms := newDelayedMetastore(5, 5)

	factory := appencryption.NewSessionFactory(
		config,
		ms,
		km,
		c,
	)
	defer factory.Close()

	randomBytes := internal.GetRandBytes(payloadSizeBytes)

	keys := make(chan interface{}, 100)

	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	go p.Provide(ctx, keys)

	stats := Stats{}

	for i := 0; ; {
		if opt.maxItems > 0 && i >= opt.maxItems {
			break
		}

		k, ok := <-keys
		if !ok {
			break
		}

		sess, err := factory.GetSession(fmt.Sprintf("partition-%v", k))
		if err != nil {
			panic(err)
		}

		_, err = sess.Encrypt(ctx, randomBytes)
		sess.Close()

		if err != nil {
			fmt.Printf("encrypt fail: i=%d, err=%v\n", i, err)
			continue
		}

		i++
		if opt.reportInterval > 0 && i%opt.reportInterval == 0 {
			metastoreStats(&stats, ms, km, uint64(i))
			r.Report(stats, opt)
		}
	}

	if opt.reportInterval == 0 {
		metastoreStats(&stats, ms, km, uint64(opt.maxItems))
		r.Report(stats, opt)
	}
}

func getConfig(opt options) *appencryption.Config {
	policy := appencryption.NewCryptoPolicy(
	// appencryption.WithRevokeCheckInterval(10 * time.Second),
	)

	policy.CreateDatePrecision = time.Minute

	switch opt.policy {
	case "session-legacy":
		policy.CacheSessions = true
		policy.SessionCacheMaxSize = opt.cacheSize
	default:
		panic(fmt.Sprintf("unknown policy: %s", opt.policy))
	}

	return &appencryption.Config{
		Policy:  policy,
		Product: product,
		Service: service,
	}
}

// metastoreStats populates the cache stats for the appencryption metastore.
func metastoreStats(stats *Stats, ms *delayedMetastore, kms *trackedKMS, requests uint64) {
	stats.RequestCount = requests

	stats.MetastoreLoadCount = uint64(ms.loadCounter.Count())
	stats.MetastoreLoadLatestCount = uint64(ms.loadLatestCounter.Count())
	stats.MetastoreStoreCount = uint64(ms.storeCounter.Count())
	stats.MetastoreOpCount = stats.MetastoreLoadCount + stats.MetastoreLoadLatestCount + stats.MetastoreStoreCount

	stats.KMSDecryptCount = uint64(kms.decryptCounter.Count())
	stats.KMSEncryptCount = uint64(kms.encryptCounter.Count())
	stats.KMSOpCount = stats.KMSDecryptCount + stats.KMSEncryptCount

	stats.OpRate = float64(stats.MetastoreOpCount+stats.KMSOpCount) / float64(stats.RequestCount)
}
