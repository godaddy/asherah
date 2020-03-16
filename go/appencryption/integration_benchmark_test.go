package appencryption_test

import (
	"fmt"
	"testing"

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
	staticKey        = "mysupersecretstaticmasterkey!!!!"
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
		for i := 0; i < b.N && pb.Next(); i++ {
			factory := appencryption.NewSessionFactory(
				config,
				metastore,
				km,
				c,
			)
			sess, _ := factory.GetSession(fmt.Sprintf(partitionID+"_%d", i))
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
		for i := 0; i < b.N && pb.Next(); i++ {
			sess, _ := factory.GetSession(fmt.Sprintf(partitionID+"_%d", i))
			randomBytes := internal.GetRandBytes(payloadSizeBytes)

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

func Benchmark_EncryptDecrypt_SameFactorySamePartition(b *testing.B) {
	km, err := kms.NewStatic(staticKey, c)
	assert.NoError(b, err)

	factory := appencryption.NewSessionFactory(
		config,
		metastore,
		km,
		c,
	)
	sess, _ := factory.GetSession(partitionID)

	defer factory.Close()
	defer sess.Close()

	b.RunParallel(func(pb *testing.PB) {
		for pb.Next() {
			randomBytes := internal.GetRandBytes(payloadSizeBytes)
			drr, err := sess.Encrypt(randomBytes)
			if err != nil {
				b.Error(err)
			}

			data, _ := sess.Decrypt(*drr)
			assert.Equal(b, randomBytes, data)
		}
	})
}
