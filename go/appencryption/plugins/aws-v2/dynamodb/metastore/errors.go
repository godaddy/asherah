package metastore

import (
	"errors"
)

// ItemDecodeError is returned when an item cannot be decoded.
var ItemDecodeError = errors.New("item decode error")
