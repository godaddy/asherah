package memcall

import "github.com/pkg/errors"

// Cleaner is the interface that groups the basic Free and Unlock methods.
type Cleaner interface {
	Freer
	Unlocker
}

// Clean attempts to clean b using c and groups any errors into a single return
// value err.
func Clean(c Cleaner, b []byte) (err error) {
	if err = c.Unlock(b); err != nil {
		err = errors.WithStack(err)
	}

	if err2 := c.Free(b); err2 != nil {
		err2 = errors.WithStack(err2)

		if err == nil {
			err = err2
		} else {
			err = errors.Wrap(err, err2.Error())
		}
	}

	return
}
