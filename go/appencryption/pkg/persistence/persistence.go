package persistence

import (
	"context"

	"github.com/godaddy/asherah/go/appencryption"
)

// LoaderFunc is an adapter to allow the use of ordinary functions as Loaders.
// If f is a function with the appropriate signature, LoaderFunc(f) is an appencryption.Loader that calls f.
type LoaderFunc func(ctx context.Context, key string) (*appencryption.DataRowRecord, error)

// Load calls f(ctx, key).
func (f LoaderFunc) Load(ctx context.Context, key string) (*appencryption.DataRowRecord, error) {
	return f(ctx, key)
}

// StorerFunc is an adapter to allow the use of ordinary functions as Storers.
// If f is a function with the appropriate signature, StorerFunc(f) is an appencryption.Storer that calls f.
type StorerFunc func(ctx context.Context, key string, d appencryption.DataRowRecord) error

// Store calls f(ctx, key, d).
func (f StorerFunc) Store(ctx context.Context, key string, d appencryption.DataRowRecord) error {
	return f(ctx, key, d)
}
