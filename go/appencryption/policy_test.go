package appencryption

import (
	"testing"

	"github.com/stretchr/testify/assert"
)

func TestNewCryptoPolicy_DefaultEvictionPolicies(t *testing.T) {
	policy := NewCryptoPolicy()
	
	// Verify that the default eviction policies are set to LRU instead of empty/simple
	assert.Equal(t, "lru", policy.IntermediateKeyCacheEvictionPolicy, "IntermediateKeyCacheEvictionPolicy should default to lru")
	assert.Equal(t, "lru", policy.SystemKeyCacheEvictionPolicy, "SystemKeyCacheEvictionPolicy should default to lru")
	assert.Equal(t, "slru", policy.SessionCacheEvictionPolicy, "SessionCacheEvictionPolicy should default to slru")
	
	// Verify other defaults are still set correctly
	assert.Equal(t, DefaultKeyCacheMaxSize, policy.IntermediateKeyCacheMaxSize)
	assert.Equal(t, DefaultKeyCacheMaxSize, policy.SystemKeyCacheMaxSize)
}

func TestCryptoPolicy_CanOverrideEvictionPolicy(t *testing.T) {
	// Test that we can still explicitly set simple cache if needed
	policy := NewCryptoPolicy()
	policy.SystemKeyCacheEvictionPolicy = "simple"
	policy.IntermediateKeyCacheEvictionPolicy = "simple"
	
	assert.Equal(t, "simple", policy.SystemKeyCacheEvictionPolicy)
	assert.Equal(t, "simple", policy.IntermediateKeyCacheEvictionPolicy)
}