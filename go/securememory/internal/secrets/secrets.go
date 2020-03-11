package secrets

// BytesWrapper contains the Bytes method that provides access to an internal byte slice.
type BytesWrapper interface {
	WithBytes(action func([]byte) error) (err error)
}
