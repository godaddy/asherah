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
	assert.Equal(t, DefaultSessionCacheSize, p.SessionCacheSize)
	assert.Equal(t, DefaultSessionCacheTTL, p.SessionCacheTTL)
}

func Test_NewCryptoPolicy_WithOptions(t *testing.T) {
	revokeCheckInterval := time.Second * 156
	expireAfterDuration := time.Second * 100
	sessionCacheSize := 42
	sessionCacheTTL := time.Second * 42

	policy := NewCryptoPolicy(
		WithRevokeCheckInterval(revokeCheckInterval),
		WithExpireAfterDuration(expireAfterDuration),
		WithNoCache(),
		WithSessionCache(),
		WithSessionCacheMaxSize(sessionCacheSize),
		WithSessionCacheTTL(sessionCacheTTL),
	)

	assert.Equal(t, revokeCheckInterval, policy.RevokeCheckInterval)
	assert.Equal(t, expireAfterDuration, policy.ExpireKeyAfter)
	assert.False(t, policy.CacheSystemKeys)
	assert.False(t, policy.CacheIntermediateKeys)
	assert.True(t, policy.CacheSessions)
	assert.Equal(t, sessionCacheSize, policy.SessionCacheSize)
	assert.Equal(t, sessionCacheTTL, policy.SessionCacheTTL)
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
