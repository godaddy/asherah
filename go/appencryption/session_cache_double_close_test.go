package appencryption

import (
	"context"
	"sync"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
)

// TestSharedEncryption_DoubleCloseProtection verifies that calling Remove() multiple times
// only calls the underlying Encryption.Close() once, preventing panics or undefined behavior.
func TestSharedEncryption_DoubleCloseProtection(t *testing.T) {
	var closeCount int
	mockEncryption := &testEncryption{
		closeFunc: func() error {
			closeCount++
			return nil
		},
	}

	sharedEnc := &sharedEncryption{
		Encryption:    mockEncryption,
		created:       time.Now(),
		accessCounter: 0, // No active users
		mu:            new(sync.Mutex),
	}
	sharedEnc.cond = sync.NewCond(sharedEnc.mu)

	// Call Remove() multiple times
	sharedEnc.Remove()
	sharedEnc.Remove()
	sharedEnc.Remove()

	// Verify Close() was called exactly once
	assert.Equal(t, 1, closeCount, "Close() should be called exactly once despite multiple Remove() calls")
}

// TestSharedEncryption_ConcurrentDoubleClose tests concurrent calls to Remove()
// to ensure no race conditions in the double-close protection.
func TestSharedEncryption_ConcurrentDoubleClose(t *testing.T) {
	var closeCount int
	var mu sync.Mutex
	mockEncryption := &testEncryption{
		closeFunc: func() error {
			mu.Lock()
			closeCount++
			mu.Unlock()
			return nil
		},
	}

	sharedEnc := &sharedEncryption{
		Encryption:    mockEncryption,
		created:       time.Now(),
		accessCounter: 0, // No active users
		mu:            new(sync.Mutex),
	}
	sharedEnc.cond = sync.NewCond(sharedEnc.mu)

	const numGoroutines = 10
	var wg sync.WaitGroup
	wg.Add(numGoroutines)

	// Launch multiple goroutines calling Remove() concurrently
	for i := 0; i < numGoroutines; i++ {
		go func() {
			defer wg.Done()
			sharedEnc.Remove()
		}()
	}

	wg.Wait()

	// Verify Close() was called exactly once despite concurrent calls
	assert.Equal(t, 1, closeCount, "Close() should be called exactly once despite concurrent Remove() calls")
}

// TestSharedEncryption_CloseAndRemove tests calling both Close() and Remove()
// to ensure they work together correctly.
func TestSharedEncryption_CloseAndRemove(t *testing.T) {
	var closeCount int
	mockEncryption := &testEncryption{
		closeFunc: func() error {
			closeCount++
			return nil
		},
	}

	sharedEnc := &sharedEncryption{
		Encryption:    mockEncryption,
		created:       time.Now(),
		accessCounter: 1, // One active user
		mu:            new(sync.Mutex),
	}
	sharedEnc.cond = sync.NewCond(sharedEnc.mu)

	// Simulate user calling Close() (decrements counter)
	err := sharedEnc.Close()
	assert.NoError(t, err)
	assert.Equal(t, 0, sharedEnc.accessCounter)

	// Now call Remove() - should close the underlying encryption
	sharedEnc.Remove()

	// Call Remove() again - should not double-close
	sharedEnc.Remove()

	// Verify Close() was called exactly once
	assert.Equal(t, 1, closeCount, "Close() should be called exactly once")
}

// testEncryption is a simple test double for the Encryption interface.
type testEncryption struct {
	closeFunc func() error
}

func (t *testEncryption) EncryptPayload(ctx context.Context, data []byte) (*DataRowRecord, error) {
	return nil, nil
}

func (t *testEncryption) DecryptDataRowRecord(ctx context.Context, record DataRowRecord) ([]byte, error) {
	return nil, nil
}

func (t *testEncryption) Close() error {
	if t.closeFunc != nil {
		return t.closeFunc()
	}
	return nil
}
