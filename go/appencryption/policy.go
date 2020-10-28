package appencryption

import (
	"time"
)

// Default values for CryptoPolicy if not overridden.
const (
	DefaultExpireAfter          = time.Hour * 24 * 90 // 90 days
	DefaultRevokedCheckInterval = time.Minute * 60
	DefaultCreateDatePrecision  = time.Minute
	DefaultSessionCacheMaxSize  = 1000
	DefaultSessionCacheDuration = time.Hour * 2
	DefaultSessionCacheEngine   = "default"
)

// CryptoPolicy contains options to customize various behaviors in the SDK.
type CryptoPolicy struct {
	// ExpireKeyAfter is used to determine when a key is considered expired based on its creation time
	// (regularly-scheduled rotation).
	ExpireKeyAfter time.Duration
	// RevokeCheckInterval controls the cache TTL (if caching is enabled) to check if a cached key has been marked as
	// revoked (irregularly-scheduled rotation).
	RevokeCheckInterval time.Duration
	// CreateDatePrecision is used to truncate a new key's creation timestamp to avoid concurrent callers from
	// excessively creating keys in race condition scenarios.
	CreateDatePrecision time.Duration
	// CacheIntermediateKeys determines whether Intermediate Keys will be cached.
	CacheIntermediateKeys bool
	// CacheSystemKeys determines whether System Keys will be cached.
	CacheSystemKeys bool
	// CacheSessions determines whether sessions will be cached.
	CacheSessions bool
	// SessionCacheMaxSize controls the maximum size of the cache if session caching is enabled.
	SessionCacheMaxSize int
	// SessionCacheDuration controls the amount of time a session will remain cached without being accessed
	// if session caching is enabled.
	SessionCacheDuration time.Duration
	// WithSessionCacheEngine determines the underlying cache implemenataion in use by the session cache
	// if session caching is enabled.
	//
	// Deprecated: multiple cache implementations are no longer supported and this option will be removed
	// in a future release.
	SessionCacheEngine string
}

// PolicyOption is used to configure a CryptoPolicy.
type PolicyOption func(*CryptoPolicy)

// WithRevokeCheckInterval sets the interval to check for revoked keys in the cache.
func WithRevokeCheckInterval(d time.Duration) PolicyOption {
	return func(policy *CryptoPolicy) {
		policy.RevokeCheckInterval = d
	}
}

// WithExpireAfterDuration sets amount of time a key is considered valid.
func WithExpireAfterDuration(d time.Duration) PolicyOption {
	return func(policy *CryptoPolicy) {
		policy.ExpireKeyAfter = d
	}
}

// WithNoCache disables caching of both System and Intermediate Keys.
func WithNoCache() PolicyOption {
	return func(policy *CryptoPolicy) {
		policy.CacheSystemKeys = false
		policy.CacheIntermediateKeys = false
	}
}

// WithSessionCache enables session caching. When used all sessions for a given partition will share underlying
// System and Intermediate Key caches.
func WithSessionCache() PolicyOption {
	return func(policy *CryptoPolicy) {
		policy.CacheSessions = true
	}
}

// WithSessionCacheMaxSize specifies the session cache max size to use if session caching is enabled.
func WithSessionCacheMaxSize(size int) PolicyOption {
	return func(policy *CryptoPolicy) {
		policy.SessionCacheMaxSize = size
	}
}

// WithSessionCacheDuration specifies the amount of time a session will remain cached without being accessed
// if session caching is enabled.
func WithSessionCacheDuration(d time.Duration) PolicyOption {
	return func(policy *CryptoPolicy) {
		policy.SessionCacheDuration = d
	}
}

// WithSessionCacheEngine determines the underlying cache implemenataion in use by the session cache
// if session caching is enabled.
//
// Deprecated: multiple cache implementations are no longer supported and this option will be removed
// in a future release.
func WithSessionCacheEngine(engine string) PolicyOption {
	return func(policy *CryptoPolicy) {
		policy.SessionCacheEngine = engine
	}
}

// NewCryptoPolicy returns a new CryptoPolicy with default values.
func NewCryptoPolicy(opts ...PolicyOption) *CryptoPolicy {
	policy := &CryptoPolicy{
		ExpireKeyAfter:        DefaultExpireAfter,
		RevokeCheckInterval:   DefaultRevokedCheckInterval,
		CreateDatePrecision:   DefaultCreateDatePrecision,
		CacheSystemKeys:       true,
		CacheIntermediateKeys: true,
		CacheSessions:         false,
		SessionCacheMaxSize:   DefaultSessionCacheMaxSize,
		SessionCacheDuration:  DefaultSessionCacheDuration,
		SessionCacheEngine:    DefaultSessionCacheEngine,
	}

	for _, opt := range opts {
		opt(policy)
	}

	return policy
}

// isKeyExpired checks if the key's created timestamp is older than the
// allowed number of days.
func isKeyExpired(created int64, expireAfter time.Duration) bool {
	return time.Now().After(time.Unix(created, 0).Add(expireAfter))
}

// newKeyTimestamp returns a unix timestamp in seconds truncated to the provided Duration.
func newKeyTimestamp(truncate time.Duration) int64 {
	if truncate > 0 {
		return time.Now().Truncate(truncate).Unix()
	}

	return time.Now().Unix()
}

// Config contains the required information to setup and use this library.
type Config struct {
	// Service is the identifier for this service.
	Service string
	// Product is the identifier for the team or group that owns the calling service.
	Product string
	// Policy contains the information on when to expire keys.
	// If no policy is provided, 90 days rotations will be set
	// as defaults.
	Policy *CryptoPolicy
}
