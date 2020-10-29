// Package log implements simple logging functionality with a focus on debug level logging. By default, logging is
// disabled and the underlying logger is a no-op implementation. Use the SetLogger helper function enable debug logging.
package log

var logger Interface = noopLogger{}

type Interface interface {
	// Debugf v using a format string.
	Debugf(format string, v ...interface{})
}

// SetLogger sets the logger used by the securememory package and enables debug level logging.
func SetLogger(l Interface) {
	logger = l
}

// Debugf writes to the log using the configured logger.
func Debugf(format string, v ...interface{}) {
	if logger != nil {
		logger.Debugf(format, v...)
	}
}

// DebugEnabled returns true if a logger has been supplied via SetLogger.
func DebugEnabled() bool {
	switch logger.(type) {
	case noopLogger, nil:
		return false
	default:
		return true
	}
}

type noopLogger struct{}

func (noopLogger) Debugf(format string, v ...interface{}) {
	// do nothing
}
