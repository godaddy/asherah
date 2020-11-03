package log

import (
	"testing"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/mock"
)

type logMock struct {
	mock.Mock
}

func (l *logMock) Debugf(f string, v ...interface{}) {
	l.Called(f, v)
}

func TestDebugf(t *testing.T) {
	current := logger

	SetLogger(noopLogger{})
	assert.False(t, DebugEnabled())

	l := new(logMock)

	SetLogger(l)
	assert.True(t, DebugEnabled())

	msg := "hello %s"
	arg := "world"

	l.On("Debugf", msg, []interface{}{arg}).Return().Once()
	Debugf(msg, arg)

	l.AssertExpectations(t)

	SetLogger(nil)
	assert.False(t, DebugEnabled())

	// The one expected call to Debugf has already occurred, verify the previously supplied logger is no longer used.
	Debugf(msg, arg)

	logger = current
}
