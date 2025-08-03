package appencryption

import (
	"fmt"
	"runtime"
	"testing"
	"time"

	"github.com/godaddy/asherah/go/appencryption/internal"
	"github.com/stretchr/testify/require"
)

// This file contains tests that intentionally create memory leaks to validate
// that our memory leak detection tests actually work and can catch real leaks.
// These tests are expected to fail when leak detection is working properly.

// TestMemoryLeakDetection_ValidationTest intentionally creates memory leaks to test our detection
func TestMemoryLeakDetection_ValidationTest(t *testing.T) {
	t.Skip("This test intentionally creates memory leaks and should only be run manually to validate leak detection")
	
	tests := []struct {
		name     string
		testFunc func(t *testing.T)
	}{
		{
			name: "Intentional_Key_Leak",
			testFunc: func(t *testing.T) {
				// Intentionally leak cached crypto keys by not closing them
				var leakedKeys []*cachedCryptoKey
				
				for i := 0; i < 1000; i++ {
					key := internal.NewCryptoKeyForTest(time.Now().Unix(), false)
					cachedKey := newCachedCryptoKey(key)
					leakedKeys = append(leakedKeys, cachedKey)
					// Intentionally NOT calling cachedKey.Close()
				}
				
				// Keep reference to prevent GC
				_ = leakedKeys
			},
		},
		{
			name: "Intentional_Session_Leak",
			testFunc: func(t *testing.T) {
				config := &Config{
					Policy:  NewCryptoPolicy(),
					Product: "leak_validation",
					Service: "test",
				}
				
				factory := NewSessionFactory(
					config,
					&benchmarkMetastore{},
					&benchmarkKMS{},
					&benchmarkCrypto{},
					WithSecretFactory(memLeakSecretFactory),
				)
				defer factory.Close()
				
				// Intentionally create sessions and not close them
				var leakedSessions []*Session
				
				for i := 0; i < 100; i++ {
					session, err := factory.GetSession("leak_test")
					require.NoError(t, err)
					leakedSessions = append(leakedSessions, session)
					// Intentionally NOT calling session.Close()
				}
				
				// Keep reference to prevent GC
				_ = leakedSessions
			},
		},
		{
			name: "Intentional_Goroutine_Leak",
			testFunc: func(t *testing.T) {
				// Create goroutines that never exit
				for i := 0; i < 10; i++ {
					go func() {
						// Infinite loop - goroutine will never exit
						for {
							time.Sleep(1 * time.Hour)
						}
					}()
				}
				
				// Give goroutines time to start
				time.Sleep(10 * time.Millisecond)
			},
		},
		{
			name: "Intentional_Memory_Growth",
			testFunc: func(t *testing.T) {
				// Allocate large amounts of memory and keep references
				var leakedMemory [][]byte
				
				for i := 0; i < 100; i++ {
					// Allocate 1MB chunks
					chunk := make([]byte, 1024*1024)
					leakedMemory = append(leakedMemory, chunk)
				}
				
				// Keep reference to prevent GC
				_ = leakedMemory
			},
		},
	}
	
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			before := getMemStats()
			beforeGoroutines := runtime.NumGoroutine()
			
			tt.testFunc(t)
			
			after := getMemStats()
			afterGoroutines := runtime.NumGoroutine()
			
			// This test should fail if leak detection is working
			checkMemoryLeaks(t, before, after, fmt.Sprintf("ValidationTest_%s", tt.name))
			
			goroutineGrowth := afterGoroutines - beforeGoroutines
			if goroutineGrowth > 5 {
				t.Errorf("Goroutine leak detected (expected): %d new goroutines", goroutineGrowth)
			}
		})
	}
}

// TestMemoryLeakDetection_EdgeCases tests edge cases that might cause false positives
func TestMemoryLeakDetection_EdgeCases(t *testing.T) {
	tests := []struct {
		name     string
		testFunc func(t *testing.T)
	}{
		{
			name: "GC_Timing_Sensitive",
			testFunc: func(t *testing.T) {
				// Create temporary allocations that should be GC'd
				for i := 0; i < 1000; i++ {
					temp := make([]byte, 1024)
					_ = temp // Use it briefly then let it go out of scope
				}
			},
		},
		{
			name: "Background_Goroutines",
			testFunc: func(t *testing.T) {
				// Create goroutines that clean themselves up
				done := make(chan bool, 10)
				
				for i := 0; i < 10; i++ {
					go func() {
						time.Sleep(10 * time.Millisecond)
						done <- true
					}()
				}
				
				// Wait for all goroutines to finish
				for i := 0; i < 10; i++ {
					<-done
				}
			},
		},
		{
			name: "Normal_Key_Operations",
			testFunc: func(t *testing.T) {
				// Perform normal operations that should not leak
				cache := newKeyCache(CacheTypeIntermediateKeys, NewCryptoPolicy())
				defer cache.Close()
				
				keyMeta := KeyMeta{ID: "edge_case_key", Created: time.Now().Unix()}
				
				for i := 0; i < 100; i++ {
					key, err := cache.GetOrLoad(keyMeta, func(meta KeyMeta) (*internal.CryptoKey, error) {
						return internal.NewCryptoKeyForTest(meta.Created, false), nil
					})
					require.NoError(t, err)
					key.Close()
				}
			},
		},
	}
	
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			before := getMemStats()
			beforeGoroutines := runtime.NumGoroutine()
			
			tt.testFunc(t)
			
			// Allow time for cleanup
			runtime.GC()
			time.Sleep(50 * time.Millisecond) 
			
			after := getMemStats()
			afterGoroutines := runtime.NumGoroutine()
			
			// These should pass (no leaks detected)
			checkMemoryLeaks(t, before, after, fmt.Sprintf("EdgeCase_%s", tt.name))
			
			goroutineGrowth := afterGoroutines - beforeGoroutines
			if goroutineGrowth > 5 {
				t.Errorf("Unexpected goroutine growth in edge case: %d new goroutines", goroutineGrowth)
			}
		})
	}
}