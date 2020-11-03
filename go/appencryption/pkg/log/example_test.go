package log_test

import (
	"fmt"

	"github.com/godaddy/asherah/go/appencryption/pkg/log"
)

// StdOut implements log.Interface and writes debug events to standard output.
type StdOut struct{}

func (StdOut) Debugf(format string, v ...interface{}) {
	fmt.Printf(format, v...)
}

// Use SetLogger to wire-up a custom logger to capture debug-level events.
func Example() {
	var l StdOut

	// Enable debug logging using our custom logger.
	log.SetLogger(l)

	// Debug logs are now enabled and will be written to standard output via our custom logger.
	log.Debugf("some debug info")
	// Output: some debug info
}
