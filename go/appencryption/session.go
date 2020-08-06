package appencryption

import (
	"context"

	"github.com/pkg/errors"
	"github.com/rcrowley/go-metrics"

	"github.com/godaddy/asherah/go/securememory"
	"github.com/godaddy/asherah/go/securememory/memguard"
)

// SessionFactory is used to create new encryption sessions and manage
// the lifetime of the intermediate keys.
type SessionFactory struct {
	sessionCache  sessionCache
	systemKeys    cache
	Config        *Config
	Metastore     Metastore
	Crypto        AEAD
	KMS           KeyManagementService
	SecretFactory securememory.SecretFactory
}

// FactoryOption is used to configure additional options in a SessionFactory.
type FactoryOption func(*SessionFactory)

// WithSecretFactory sets the factory to use for creating Secrets
func WithSecretFactory(f securememory.SecretFactory) FactoryOption {
	return func(factory *SessionFactory) {
		factory.SecretFactory = f
	}
}

// WithMetrics enables or disables metrics.
func WithMetrics(enabled bool) FactoryOption {
	return func(factory *SessionFactory) {
		if !enabled {
			metrics.DefaultRegistry.UnregisterAll()
		}
	}
}

// NewSessionFactory creates a new session factory with default implementations.
func NewSessionFactory(config *Config, store Metastore, kms KeyManagementService, crypto AEAD, opts ...FactoryOption) *SessionFactory {
	if config.Policy == nil {
		config.Policy = NewCryptoPolicy()
	}

	var skCache cache
	if config.Policy.CacheSystemKeys {
		skCache = newKeyCache(config.Policy)
	} else {
		skCache = new(neverCache)
	}

	factory := &SessionFactory{
		systemKeys:    skCache,
		Config:        config,
		Metastore:     store,
		Crypto:        crypto,
		KMS:           kms,
		SecretFactory: new(memguard.SecretFactory),
	}

	if config.Policy.CacheSessions {
		factory.sessionCache = newSessionCache(factory.newSession, config.Policy)
	}

	for _, opt := range opts {
		opt(factory)
	}

	return factory
}

// Close will close any open resources owned by this factory (e.g. cache of system keys). It should be called
// when the factory is no longer required
func (f *SessionFactory) Close() error {
	return f.systemKeys.Close()
}

// GetSession returns a new session for the provided partition ID.
func (f *SessionFactory) GetSession(id string) (*Session, error) {
	if id == "" {
		return nil, errors.New("partition id cannot be empty")
	}

	if f.Config.Policy.CacheSessions {
		return f.sessionCache.Get(id)
	}

	return f.newSession(id)
}

func (f *SessionFactory) newSession(id string) (*Session, error) {
	return &Session{
		encryption: &envelopeEncryption{
			partition:        f.newPartition(id),
			Metastore:        f.Metastore,
			KMS:              f.KMS,
			Policy:           f.Config.Policy,
			Crypto:           f.Crypto,
			SecretFactory:    f.SecretFactory,
			systemKeys:       f.systemKeys,
			intermediateKeys: f.newIKCache(),
		}}, nil
}

func (f *SessionFactory) newPartition(id string) partition {
	if v, ok := f.Metastore.(interface{ GetRegionSuffix() string }); ok && len(v.GetRegionSuffix()) > 0 {
		return newSuffixedPartition(id, f.Config.Service, f.Config.Product, v.GetRegionSuffix())
	}

	return newPartition(id, f.Config.Service, f.Config.Product)
}

func (f *SessionFactory) newIKCache() cache {
	if f.Config.Policy.CacheIntermediateKeys {
		return newKeyCache(f.Config.Policy)
	}

	return new(neverCache)
}

// Session is used to encrypt and decrypt data related to a specific partition ID.
type Session struct {
	encryption Encryption
}

// Encrypt encrypts a provided slice of bytes and returns a DataRowRecord, which contains required
// information to decrypt the data in the future.
func (s *Session) Encrypt(data []byte) (*DataRowRecord, error) {
	return s.EncryptContext(context.Background(), data)
}

// EncryptContext encrypts a provided slice of bytes and returns a DataRowRecord, which contains required
// information to decrypt the data in the future. It also takes a context used for cancellation.
func (s *Session) EncryptContext(ctx context.Context, data []byte) (*DataRowRecord, error) {
	return s.encryption.EncryptPayload(ctx, data)
}

// Decrypt decrypts a DataRowRecord key and returns the original byte slice provided to the encrypt function.
func (s *Session) Decrypt(d DataRowRecord) ([]byte, error) {
	return s.DecryptContext(context.Background(), d)
}

// DecryptContext decrypts a DataRowRecord and returns the original byte slice provided to the encrypt function.
// It also accepts a context for cancellation.
func (s *Session) DecryptContext(ctx context.Context, d DataRowRecord) ([]byte, error) {
	return s.encryption.DecryptDataRowRecord(ctx, d)
}

// Close will close any open resources owned by this session (e.g. cache of keys). It should be called
// as soon as it's no longer in use.
func (s *Session) Close() error {
	return s.encryption.Close()
}
