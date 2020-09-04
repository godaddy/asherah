package appencryption_test

import (
	"fmt"
	"math/rand"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/internal"
	"github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
	"github.com/godaddy/asherah/go/appencryption/pkg/kms"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence"
)

const (
	product          = "enclibrary"
	service          = "asherah"
	partitionID      = "123456"
	staticKey        = "thisIsAStaticMasterKeyForTesting"
	payloadSizeBytes = 100
)

var (
	c      = aead.NewAES256GCM()
	config = &appencryption.Config{
		Policy:  appencryption.NewCryptoPolicy(),
		Product: product,
		Service: service,
	}
	metastore = persistence.NewMemoryMetastore()
	caches    = [...]string{
		// "mango", // Disabled until data race is resolved upstream (https://github.com/goburrow/cache/issues/21)
		"ristretto",
	}
)

func BenchmarkSession_Encrypt(b *testing.B) {
	km, err := kms.NewStatic(staticKey, c)
	assert.NoError(b, err)

	factory := appencryption.NewSessionFactory(
		config,
		metastore,
		km,
		c,
	)

	randomBytes := make([][]byte, b.N)
	for i := 0; i < b.N; i++ {
		randomBytes[i] = internal.GetRandBytes(payloadSizeBytes)
	}

	sess, _ := factory.GetSession(partitionID)

	b.ResetTimer()

	for i := 0; i < b.N; i++ {
		bytes := randomBytes[i]

		if _, err := sess.Encrypt(bytes); err != nil {
			b.Error(err)
		}
	}
}

func Benchmark_EncryptDecrypt_MultiFactorySamePartition(b *testing.B) {
	km, err := kms.NewStatic(staticKey, c)
	assert.NoError(b, err)

	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			factory := appencryption.NewSessionFactory(
				config,
				metastore,
				km,
				c,
			)
			sess, _ := factory.GetSession(partitionID)
			randomBytes := internal.GetRandBytes(payloadSizeBytes)

			drr, err := sess.Encrypt(randomBytes)
			if err != nil {
				b.Error(err)
			}

			data, _ := sess.Decrypt(*drr)
			assert.Equal(b, randomBytes, data)

			sess.Close()
			factory.Close()
		}
	})
}

func Benchmark_EncryptDecrypt_MultiFactoryUniquePartition(b *testing.B) {
	km, err := kms.NewStatic(staticKey, c)
	assert.NoError(b, err)

	b.RunParallel(func(pb *testing.PB) {
		zipf := newZipf(10, appencryption.DefaultSessionCacheMaxSize*16)

		for i := 0; i < b.N && pb.Next(); i++ {
			factory := appencryption.NewSessionFactory(
				config,
				metastore,
				km,
				c,
			)
			sess, _ := factory.GetSession(fmt.Sprintf(partitionID+"_%d", zipf()))
			randomBytes := internal.GetRandBytes(payloadSizeBytes)

			drr, err := sess.Encrypt(randomBytes)
			if err != nil {
				b.Error(err)
			}

			data, _ := sess.Decrypt(*drr)
			assert.Equal(b, randomBytes, data)

			sess.Close()
			factory.Close()
		}
	})
}

func Benchmark_EncryptDecrypt_SameFactoryUniquePartition(b *testing.B) {
	km, err := kms.NewStatic(staticKey, c)
	assert.NoError(b, err)

	factory := appencryption.NewSessionFactory(
		config,
		metastore,
		km,
		c,
	)
	defer factory.Close()

	b.RunParallel(func(pb *testing.PB) {
		zipf := newZipf(10, appencryption.DefaultSessionCacheMaxSize*16)

		for pb.Next() {
			partition := fmt.Sprintf(partitionID+"_%d", zipf())
			randomBytes := internal.GetRandBytes(payloadSizeBytes)

			sess, _ := factory.GetSession(partition)

			drr, err := sess.Encrypt(randomBytes)
			if err != nil {
				b.Error(err)
			}

			data, _ := sess.Decrypt(*drr)
			assert.Equal(b, randomBytes, data)

			sess.Close()
		}
	})
}

func newZipf(v float64, n uint64) func() uint64 {
	zipfS := 1.7
	z := rand.NewZipf(rand.New(rand.NewSource(time.Now().UnixNano())), zipfS, v, n)

	return z.Uint64
}

func Benchmark_EncryptDecrypt_SameFactoryUniquePartition_WithSessionCache(b *testing.B) {
	capacity := appencryption.DefaultSessionCacheMaxSize

	for i := range caches {
		engine := caches[i]

		subtest := fmt.Sprintf("WithEngine %s", engine)
		b.Run(subtest, func(bb *testing.B) {
			conf := &appencryption.Config{
				Policy: appencryption.NewCryptoPolicy(
					appencryption.WithSessionCache(),
					appencryption.WithSessionCacheEngine(engine),
				),
				Product: product,
				Service: service,
			}

			km, err := kms.NewStatic(staticKey, c)
			assert.NoError(bb, err)

			factory := appencryption.NewSessionFactory(
				conf,
				metastore,
				km,
				c,
			)
			defer factory.Close()

			bb.RunParallel(func(pb *testing.PB) {
				zipf := newZipf(10, uint64(capacity)*16)

				for pb.Next() {
					partition := fmt.Sprintf(partitionID+"_%d", zipf())
					randomBytes := internal.GetRandBytes(payloadSizeBytes)

					sess, _ := factory.GetSession(partition)

					drr, err := sess.Encrypt(randomBytes)
					if err != nil {
						bb.Error(err)
					}

					data, _ := sess.Decrypt(*drr)
					assert.Equal(bb, randomBytes, data)

					sess.Close()
				}
			})
		})
	}
}

func Benchmark_EncryptDecrypt_SameFactorySamePartition(b *testing.B) {
	km, err := kms.NewStatic(staticKey, c)
	assert.NoError(b, err)

	factory := appencryption.NewSessionFactory(
		config,
		metastore,
		km,
		c,
	)
	defer factory.Close()

	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			randomBytes := internal.GetRandBytes(payloadSizeBytes)

			sess, _ := factory.GetSession(partitionID)

			drr, err := sess.Encrypt(randomBytes)
			if err != nil {
				b.Error(err)
			}

			data, _ := sess.Decrypt(*drr)
			assert.Equal(b, randomBytes, data)

			sess.Close()
		}
	})
}

func Benchmark_EncryptDecrypt_SameFactorySamePartition_WithSessionCache(b *testing.B) {
	for i := range caches {
		engine := caches[i]

		b.Run(fmt.Sprintf("WithEngine %s", engine), func(bb *testing.B) {
			km, err := kms.NewStatic(staticKey, c)
			assert.NoError(bb, err)

			conf := &appencryption.Config{
				Policy: appencryption.NewCryptoPolicy(
					appencryption.WithSessionCache(),
					appencryption.WithSessionCacheEngine(engine),
				),
				Product: product,
				Service: service,
			}

			factory := appencryption.NewSessionFactory(
				conf,
				metastore,
				km,
				c,
			)
			defer factory.Close()

			bb.RunParallel(func(pb *testing.PB) {
				for pb.Next() {
					randomBytes := internal.GetRandBytes(payloadSizeBytes)

					sess, _ := factory.GetSession(partitionID)

					drr, err := sess.Encrypt(randomBytes)
					if err != nil {
						bb.Error(err)
					}

					data, _ := sess.Decrypt(*drr)
					assert.Equal(bb, randomBytes, data)

					sess.Close()
				}
			})
		})
	}
}
