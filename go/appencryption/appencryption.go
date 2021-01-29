// Package appencryption contains the implementation to securely persist
// and encrypt data in the public cloud. Your main interaction with
// the library will most likely be the SessionFactory which should be
// created on application start up and stored for the lifetime of
// the app.
//
// A session should closed as close as possible to the creation of the
// session. It should also be short lived to avoid running into the
// limits on the amount of memory that can be locked. See mlock documentation
// on how to set/check the current limits. It can also be checked using
// ulimit.
package appencryption

import "context"

// Encryption implements the required methods to perform encryption/decryption on a payload
type Encryption interface {
	// EncryptPayload encrypts a provided slice of bytes and returns a DataRowRecord which contains
	// required information to decrypt the data in the future.
	EncryptPayload(ctx context.Context, data []byte) (*DataRowRecord, error)
	// DecryptDataRowRecord decrypts a DataRowRecord key and returns the original byte slice
	// provided to the encrypt function.
	DecryptDataRowRecord(ctx context.Context, d DataRowRecord) ([]byte, error)
	// Close frees up any resources. It should be called as soon as an instance is
	// no longer in use.
	Close() error
}

// KeyManagementService contains the logic required to encrypt a system key
// with a master key.
type KeyManagementService interface {
	// EncryptKey takes in an unencrypted byte slice and encrypts it with the master key.
	// The returned value should then be inserted into the Metastore before being
	// used.
	EncryptKey(context.Context, []byte) ([]byte, error)
	// DecryptKey decrypts the encrypted byte slice using the master key.
	DecryptKey(context.Context, []byte) ([]byte, error)
}

// Metastore implements the required methods to retrieve an encryption key
// from it's storage.
type Metastore interface {
	// Load retrieves a specific key by id and created timestamp.
	// The return value will be nil if not already present.
	Load(ctx context.Context, id string, created int64) (*EnvelopeKeyRecord, error)
	// LoadLatest returns the latest key matching the provided ID.
	// The return value will be nil if not already present.
	LoadLatest(ctx context.Context, id string) (*EnvelopeKeyRecord, error)
	// Store attempts to insert the key into the metastore if one is not
	// already present. If a key exists, the method will return false. If
	// one is not present, the value will be inserted and we return true.
	Store(ctx context.Context, id string, created int64, envelope *EnvelopeKeyRecord) (bool, error)
}

// AEAD contains the functions required to encrypt
// and decrypt data using a specific cipher.
type AEAD interface {
	// Encrypt encrypts data using the provided key bytes.
	Encrypt(data, key []byte) ([]byte, error)
	// Decrypt decrypts data using the provided key bytes.
	Decrypt(data, key []byte) ([]byte, error)
}

// Loader declares the behavior for loading data from a persistence store.
type Loader interface {
	// Load returns a DataRowRecord corresponding to the specified key, if found, along with any errors encountered.
	Load(ctx context.Context, key interface{}) (*DataRowRecord, error)
}

// Storer declares the behavior for storing data in a persistence store.
type Storer interface {
	// Store persists the DataRowRecord and returns its associated key for future lookup (e.g. UUID, etc.).
	Store(ctx context.Context, d DataRowRecord) (interface{}, error)
}

// AES256KeySize is the size of the AES key used by the AEAD implementation
const AES256KeySize int = 32
