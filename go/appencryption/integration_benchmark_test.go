package appencryption_test

import (
	"context"
	"fmt"
	"log"
	"math/rand"
	"os"
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
	zipfSeed  = time.Now().UnixNano()
)

func TestMain(m *testing.M) {
	log.Printf("random seed: %d\n", zipfSeed)

	code := m.Run()

	os.Exit(code)
}

func Benchmark_Encrypt(b *testing.B) {
	km, err := kms.NewStatic(staticKey, c)
	assert.NoError(b, err)

	defer km.Close()

	factory := appencryption.NewSessionFactory(
		config,
		metastore,
		km,
		c,
	)
	defer factory.Close()

	randomBytes := make([][]byte, b.N)
	for i := 0; i < b.N; i++ {
		randomBytes[i] = internal.GetRandBytes(payloadSizeBytes)
	}

	sess, _ := factory.GetSession(partitionID)
	defer sess.Close()

	b.ResetTimer()

	ctx := context.Background()

	for i := 0; i < b.N; i++ {
		bytes := randomBytes[i]

		if _, err := sess.Encrypt(ctx, bytes); err != nil {
			b.Error(err)
		}
	}
}

func Benchmark_EncryptDecrypt_MultiFactorySamePartition(b *testing.B) {
	km, err := kms.NewStatic(staticKey, c)
	assert.NoError(b, err)

	defer km.Close()

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
			ctx := context.Background()

			drr, err := sess.Encrypt(ctx, randomBytes)
			if err != nil {
				b.Error(err)
			}

			data, _ := sess.Decrypt(ctx, *drr)
			assert.Equal(b, randomBytes, data)

			sess.Close()
			factory.Close()
		}
	})
}

func Benchmark_EncryptDecrypt_MultiFactoryUniquePartition(b *testing.B) {
	km, err := kms.NewStatic(staticKey, c)
	assert.NoError(b, err)

	defer km.Close()

	b.RunParallel(func(pb *testing.PB) {
		zipf := newZipf()

		for i := 0; i < b.N && pb.Next(); i++ {
			factory := appencryption.NewSessionFactory(
				config,
				metastore,
				km,
				c,
			)
			sess, _ := factory.GetSession(fmt.Sprintf(partitionID+"_%d", zipf()))
			randomBytes := internal.GetRandBytes(payloadSizeBytes)
			ctx := context.Background()

			drr, err := sess.Encrypt(ctx, randomBytes)
			if err != nil {
				b.Error(err)
			}

			data, _ := sess.Decrypt(ctx, *drr)
			assert.Equal(b, randomBytes, data)

			sess.Close()
			factory.Close()
		}
	})
}

func Benchmark_EncryptDecrypt_SameFactoryUniquePartition(b *testing.B) {
	km, err := kms.NewStatic(staticKey, c)
	assert.NoError(b, err)

	defer km.Close()

	metastore := persistence.NewMemoryMetastore()

	factory := appencryption.NewSessionFactory(
		config,
		metastore,
		km,
		c,
	)
	defer factory.Close()

	b.RunParallel(func(pb *testing.PB) {
		zipf := newZipf()

		for pb.Next() {
			partition := fmt.Sprintf(partitionID+"_%d", zipf())
			randomBytes := internal.GetRandBytes(payloadSizeBytes)

			sess, _ := factory.GetSession(partition)
			ctx := context.Background()

			drr, err := sess.Encrypt(ctx, randomBytes)
			if err != nil {
				b.Error(err)
			}

			data, _ := sess.Decrypt(ctx, *drr)
			assert.Equal(b, randomBytes, data)

			sess.Close()
		}
	})
}

func newZipf() func() uint64 {
	cap := uint64(appencryption.DefaultSessionCacheMaxSize)
	zipfS := 1.0001
	v := 10.0
	n := cap * 32

	z := rand.NewZipf(rand.New(rand.NewSource(zipfSeed)), zipfS, v, n)

	return z.Uint64
}

func Benchmark_EncryptDecrypt_SameFactoryUniquePartition_WithSessionCache(b *testing.B) {
	km, err := kms.NewStatic(staticKey, c)
	assert.NoError(b, err)

	defer km.Close()

	factory := appencryption.NewSessionFactory(
		&appencryption.Config{
			Policy: appencryption.NewCryptoPolicy(
				appencryption.WithSessionCache(),
				appencryption.WithSessionCacheMaxSize(1000),
			),
			Product: product,
			Service: service,
		},
		metastore,
		km,
		c,
	)

	defer factory.Close()

	b.RunParallel(func(pb *testing.PB) {
		zipf := newZipf()

		for pb.Next() {
			func() {
				partition := fmt.Sprintf("%d", zipf())

				randomBytes := internal.GetRandBytes(payloadSizeBytes)

				sess, _ := factory.GetSession(partition)
				ctx := context.Background()

				drr, err := sess.Encrypt(ctx, randomBytes)
				if err != nil {
					b.Error(err)
				}

				data, _ := sess.Decrypt(ctx, *drr)
				assert.Equal(b, randomBytes, data)

				sess.Close()
			}()
		}
	})
}

func Benchmark_EncryptDecrypt_SameFactorySamePartition(b *testing.B) {
	km, err := kms.NewStatic(staticKey, c)
	assert.NoError(b, err)

	defer km.Close()

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
			ctx := context.Background()

			drr, err := sess.Encrypt(ctx, randomBytes)
			if err != nil {
				b.Error(err)
			}

			data, _ := sess.Decrypt(ctx, *drr)
			assert.Equal(b, randomBytes, data)

			sess.Close()
		}
	})
}

func Benchmark_EncryptDecrypt_SameFactorySamePartition_WithSessionCache(b *testing.B) {
	km, err := kms.NewStatic(staticKey, c)
	assert.NoError(b, err)

	defer km.Close()

	conf := &appencryption.Config{
		Policy: appencryption.NewCryptoPolicy(
			appencryption.WithSessionCache(),
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

	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			randomBytes := internal.GetRandBytes(payloadSizeBytes)

			sess, _ := factory.GetSession(partitionID)
			ctx := context.Background()

			drr, err := sess.Encrypt(ctx, randomBytes)
			if err != nil {
				b.Error(err)
			}

			data, _ := sess.Decrypt(ctx, *drr)
			assert.Equal(b, randomBytes, data)

			sess.Close()
		}
	})
}
