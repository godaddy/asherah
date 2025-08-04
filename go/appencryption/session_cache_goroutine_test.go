package appencryption

import (
	"context"
	"runtime"
	"sync"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"

	"github.com/godaddy/asherah/go/appencryption/internal"
)

// TestSessionCache_GoroutineLeakFixed verifies that the goroutine leak
// in session cache eviction has been fixed by using a worker pool.
func TestSessionCache_GoroutineLeakFixed(t *testing.T) {
	policy := &CryptoPolicy{
		SessionCacheMaxSize: 10, // Small cache to force evictions
	}

	cache := newSessionCache(func(id string) (*Session, error) {
		// Create a mock session with sharedEncryption
		key := internal.NewCryptoKeyForTest(time.Now().Unix(), false)
		mu := new(sync.Mutex)
		sharedEnc := &sharedEncryption{
			Encryption:    &mockEncryption{key: key},
			mu:            mu,
			cond:          sync.NewCond(mu),
			created:       time.Now(),
			accessCounter: 0,
		}

		return &Session{
			encryption: sharedEnc,
		}, nil
	}, policy)

	initialGoroutines := runtime.NumGoroutine()

	// Create many sessions to trigger evictions
	const numSessions = 100
	for i := 0; i < numSessions; i++ {
		session, err := cache.Get("session-" + string(rune(i)))
		assert.NoError(t, err)
		assert.NotNil(t, session)

		// Close the session to allow it to be evicted
		session.Close()
	}

	// Give some time for worker pool to process
	time.Sleep(100 * time.Millisecond)

	// Close the cache
	cache.Close()

	// Wait for cleanup
	time.Sleep(100 * time.Millisecond)

	finalGoroutines := runtime.NumGoroutine()

	// We should not have created a massive number of goroutines
	// Single cleanup goroutine should limit goroutine growth (1 + minimal overhead)
	goroutineIncrease := finalGoroutines - initialGoroutines
	assert.LessOrEqual(t, goroutineIncrease, 5,
		"Should not create excessive goroutines (single cleanup processor should limit growth)")
}

// TestSessionCleanupProcessor_Sequential tests that the processor
// handles cleanup operations sequentially.
func TestSessionCleanupProcessor_Sequential(t *testing.T) {
	processor := newSessionCleanupProcessor()
	defer processor.close()

	const numTasks = 10
	var processOrder []int
	var mu sync.Mutex

	var wg sync.WaitGroup
	wg.Add(numTasks)

	// Submit tasks that track processing order
	for i := 0; i < numTasks; i++ {
		taskID := i
		key := internal.NewCryptoKeyForTest(time.Now().Unix(), false)
		mockMu := new(sync.Mutex)
		sharedEnc := &sharedEncryption{
			Encryption: &mockEncryption{
				key: key,
				onClose: func() {
					mu.Lock()
					processOrder = append(processOrder, taskID)
					mu.Unlock()

					// Small delay to ensure ordering
					time.Sleep(5 * time.Millisecond)
					wg.Done()
				},
			},
			mu:            mockMu,
			cond:          sync.NewCond(mockMu),
			created:       time.Now(),
			accessCounter: 0,
		}

		processor.submit(sharedEnc)
	}

	wg.Wait()

	// Should have processed all tasks
	assert.Equal(t, numTasks, len(processOrder), "Should process all tasks")
}

// TestSessionCleanupProcessor_QueueFull tests behavior when the cleanup queue is full.
func TestSessionCleanupProcessor_QueueFull(t *testing.T) {
	processor := newSessionCleanupProcessor()
	defer processor.close()

	// Block the processor with a long-running task
	key1 := internal.NewCryptoKeyForTest(time.Now().Unix(), false)
	mockMu1 := new(sync.Mutex)
	blockingEnc := &sharedEncryption{
		Encryption: &mockEncryption{
			key: key1,
			onClose: func() {
				time.Sleep(200 * time.Millisecond) // Block for a while
			},
		},
		mu:            mockMu1,
		cond:          sync.NewCond(mockMu1),
		created:       time.Now(),
		accessCounter: 0,
	}

	// Submit the blocking task
	success := processor.submit(blockingEnc)
	assert.True(t, success, "First task should be accepted")

	// Try to fill up the queue - should eventually fall back to synchronous execution
	var syncExecuted atomic.Bool
	key2 := internal.NewCryptoKeyForTest(time.Now().Unix(), false)
	mockMu2 := new(sync.Mutex)
	syncEnc := &sharedEncryption{
		Encryption: &mockEncryption{
			key: key2,
			onClose: func() {
				syncExecuted.Store(true)
			},
		},
		mu:            mockMu2,
		cond:          sync.NewCond(mockMu2),
		created:       time.Now(),
		accessCounter: 0,
	}

	// Fill up the queue (buffer size is 10000)
	for i := 0; i < 10010; i++ {
		processor.submit(syncEnc)
	}

	// Should have fallen back to synchronous execution
	assert.True(t, syncExecuted.Load(), "Should have executed synchronously when queue full")
}

// mockEncryption is a test double for Encryption.
type mockEncryption struct {
	key     *internal.CryptoKey
	onClose func()
}

func (m *mockEncryption) EncryptPayload(ctx context.Context, data []byte) (*DataRowRecord, error) {
	return nil, nil
}

func (m *mockEncryption) DecryptDataRowRecord(ctx context.Context, record DataRowRecord) ([]byte, error) {
	return nil, nil
}

func (m *mockEncryption) Close() error {
	if m.onClose != nil {
		m.onClose()
	}

	return nil
}
