package appencryption

import (
	"testing"
	"time"

	"github.com/stretchr/testify/assert"

	"github.com/godaddy/asherah/go/appencryption/internal"
)

func Test_NewCryptoPolicy_WithDefaults(t *testing.T) {
	p := NewCryptoPolicy()

	assert.Equal(t, DefaultExpireAfter, p.ExpireKeyAfter)
	assert.Equal(t, DefaultRevokedCheckInterval, p.RevokeCheckInterval)
	assert.Equal(t, DefaultCreateDatePrecision, p.CreateDatePrecision)
	assert.True(t, p.CacheSystemKeys)
	assert.True(t, p.CacheIntermediateKeys)
	assert.False(t, p.CacheSessions)
	assert.Equal(t, DefaultSessionCacheMaxSize, p.SessionCacheMaxSize)
	assert.Equal(t, DefaultSessionCacheDuration, p.SessionCacheDuration)
	assert.Equal(t, DefaultSessionCacheEngine, p.SessionCacheEngine)
}

func Test_NewCryptoPolicy_WithOptions(t *testing.T) {
	revokeCheckInterval := time.Second * 156
	expireAfterDuration := time.Second * 100
	sessionCacheMaxSize := 42
	sessionCacheDuration := time.Second * 42
	sessionCacheEngine := "deprecated"

	policy := NewCryptoPolicy(
		WithRevokeCheckInterval(revokeCheckInterval),
		WithExpireAfterDuration(expireAfterDuration),
		WithNoCache(),
		WithSessionCache(),
		WithSessionCacheMaxSize(sessionCacheMaxSize),
		WithSessionCacheDuration(sessionCacheDuration),
		WithSessionCacheEngine(sessionCacheEngine),
	)

	assert.Equal(t, revokeCheckInterval, policy.RevokeCheckInterval)
	assert.Equal(t, expireAfterDuration, policy.ExpireKeyAfter)
	assert.False(t, policy.CacheSystemKeys)
	assert.False(t, policy.CacheIntermediateKeys)
	assert.True(t, policy.CacheSessions)
	assert.Equal(t, sessionCacheMaxSize, policy.SessionCacheMaxSize)
	assert.Equal(t, sessionCacheDuration, policy.SessionCacheDuration)
	assert.Equal(t, sessionCacheEngine, policy.SessionCacheEngine)
}

func Test_IsKeyExpired(t *testing.T) {
	tests := []struct {
		Name            string
		CreatedAt       time.Time
		ExpireAfterDays int
		Expect          bool
	}{
		{
			Name:            "should be expired",
			CreatedAt:       time.Now().Add(-24 * time.Hour * 10),
			ExpireAfterDays: 1,
			Expect:          true,
		},
		{
			Name:            "should not be expired",
			CreatedAt:       time.Now().Add(-24 * time.Hour * 1),
			ExpireAfterDays: 90,
			Expect:          false,
		},
	}

	for i := range tests {
		tt := tests[i]
		t.Run(tt.Name, func(t *testing.T) {
			verify := assert.New(t)

			key := internal.NewCryptoKeyForTest(tt.CreatedAt.Unix(), false)

			verify.Equal(tt.Expect, isKeyExpired(key.Created(), time.Hour*24*time.Duration(tt.ExpireAfterDays)))
		})
	}
}

func Test_NewKeyTimestamp(t *testing.T) {
	now := time.Now()

	truncated := time.Unix(newKeyTimestamp(time.Minute), 0)

	assert.Equal(t, now.Year(), truncated.Year())
	assert.Equal(t, now.Day(), truncated.Day())
	assert.Equal(t, now.Month(), truncated.Month())
	assert.Equal(t, now.Minute(), truncated.Minute())
	assert.Equal(t, 0, truncated.Second())
}

func TestNewKeyTimestamp_NoTruncate(t *testing.T) {
	now := time.Now()

	truncated := time.Unix(newKeyTimestamp(0), 0)

	assert.Equal(t, now.Year(), truncated.Year())
	assert.Equal(t, now.Day(), truncated.Day())
	assert.Equal(t, now.Month(), truncated.Month())
	assert.Equal(t, now.Minute(), truncated.Minute())
	assert.Equal(t, now.Second(), truncated.Second())
}
