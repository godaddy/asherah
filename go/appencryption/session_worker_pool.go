package appencryption

import (
	"sync"
	"time"

	"github.com/godaddy/asherah/go/appencryption/pkg/log"
)


// sessionCleanupProcessor manages a single goroutine to handle session cleanup.
// This provides minimal overhead for Lambda while preventing unbounded goroutines.
type sessionCleanupProcessor struct {
	workChan chan *sharedEncryption
	done     chan struct{}
	once     sync.Once
}

// newSessionCleanupProcessor creates a single-goroutine cleanup processor.
func newSessionCleanupProcessor() *sessionCleanupProcessor {
	p := &sessionCleanupProcessor{
		workChan: make(chan *sharedEncryption, 10000), // Large buffer for big bursts
		done:     make(chan struct{}),
	}

	// Start single cleanup goroutine
	go p.processor()

	return p
}

// processor handles cleanup tasks sequentially.
func (p *sessionCleanupProcessor) processor() {
	for {
		select {
		case encryption := <-p.workChan:
			log.Debugf("processing session cleanup")
			encryption.Remove()
		case <-p.done:
			// Drain remaining work
			for {
				select {
				case encryption := <-p.workChan:
					encryption.Remove()
				default:
					return
				}
			}
		}
	}
}

// submit adds a session for cleanup.
func (p *sessionCleanupProcessor) submit(encryption *sharedEncryption) bool {
	defer func() {
		if r := recover(); r != nil {
			// Channel was closed, fall back to synchronous cleanup
			log.Debugf("session cleanup processor closed, performing synchronous cleanup")
			encryption.Remove()
		}
	}()

	select {
	case p.workChan <- encryption:
		return true
	default:
		// Queue is full, fall back to synchronous cleanup
		log.Debugf("session cleanup queue full, performing synchronous cleanup")
		encryption.Remove()
		return false
	}
}

// close shuts down the cleanup processor.
func (p *sessionCleanupProcessor) close() {
	p.once.Do(func() {
		close(p.done)
		// Don't need to wait since processor will drain and exit
	})
}

// waitForEmpty blocks until the work queue is empty.
// This is primarily used for testing to ensure cleanup has completed.
func (p *sessionCleanupProcessor) waitForEmpty() {
	// Wait for queue to drain
	for i := 0; i < 200; i++ { // max 2 seconds
		if len(p.workChan) == 0 {
			// Give processor more time to finish processing any in-flight items
			time.Sleep(time.Millisecond * 100)
			return
		}
		time.Sleep(time.Millisecond * 10)
	}
}

// globalSessionCleanupProcessor is the shared cleanup processor for all session caches.
// Using a global processor prevents multiple caches from creating their own processors.
var (
	globalSessionCleanupProcessor     *sessionCleanupProcessor
	globalSessionCleanupProcessorOnce sync.Once
	globalSessionCleanupProcessorMu   sync.Mutex
)

// getSessionCleanupProcessor returns the global session cleanup processor, creating it if needed.
func getSessionCleanupProcessor() *sessionCleanupProcessor {
	globalSessionCleanupProcessorOnce.Do(func() {
		// Single goroutine processor - minimal overhead for Lambda,
		// still prevents unbounded goroutine creation for servers
		globalSessionCleanupProcessor = newSessionCleanupProcessor()
	})

	return globalSessionCleanupProcessor
}

// resetGlobalSessionCleanupProcessor resets the global processor for testing.
// This should only be used in tests.
func resetGlobalSessionCleanupProcessor() {
	globalSessionCleanupProcessorMu.Lock()
	defer globalSessionCleanupProcessorMu.Unlock()

	if globalSessionCleanupProcessor != nil {
		globalSessionCleanupProcessor.close()
	}

	globalSessionCleanupProcessor = nil
	globalSessionCleanupProcessorOnce = sync.Once{}
}
