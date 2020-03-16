package persistence

import (
	"context"
	"sort"
	"sync"

	"github.com/godaddy/asherah/go/appencryption"
)

// Verify MemoryMetastore implements the keystore interface.
var _ appencryption.Metastore = (*MemoryMetastore)(nil)

// MemoryMetastore is an in-memory implementation of a Metastore.
// NOTE: It should not be used in production and is for testing only!
type MemoryMetastore struct {
	sync.RWMutex

	Envelopes map[string]map[int64]*appencryption.EnvelopeKeyRecord
}

// NewMemoryMetastore returns a new in-memory metastore.
func NewMemoryMetastore() *MemoryMetastore {
	return &MemoryMetastore{
		Envelopes: make(map[string]map[int64]*appencryption.EnvelopeKeyRecord),
	}
}

// Load retrieves a specific key by id and created timestamp.
// The return value will be nil if not already present.
func (s *MemoryMetastore) Load(_ context.Context, keyID string, created int64) (*appencryption.EnvelopeKeyRecord, error) {
	s.RLock()
	defer s.RUnlock()

	if ret, ok := s.Envelopes[keyID][created]; ok {
		return ret, nil
	}

	return nil, nil
}

// LoadLatest returns the latest key matching the provided ID.
// The return value will be nil if not already present.
func (s *MemoryMetastore) LoadLatest(_ context.Context, keyID string) (*appencryption.EnvelopeKeyRecord, error) {
	s.RLock()
	defer s.RUnlock()

	if keyIDMap, ok := s.Envelopes[keyID]; ok {
		// Sort submap by key since it is the created time
		var createdKeys []int64
		for created := range keyIDMap {
			createdKeys = append(createdKeys, created)
		}

		sort.Slice(createdKeys, func(i, j int) bool { return createdKeys[i] < createdKeys[j] })

		latestCreated := createdKeys[len(createdKeys)-1]
		if ret, ok := keyIDMap[latestCreated]; ok {
			return ret, nil
		}
	}

	return nil, nil
}

// Store attempts to insert the key into the metastore if one is not
// already present. If a key exists, the method will return false. If
// one is not present, the value will be inserted and we return true.
func (s *MemoryMetastore) Store(_ context.Context, keyID string, created int64, envelope *appencryption.EnvelopeKeyRecord) (bool, error) {
	s.Lock()
	defer s.Unlock()

	if _, ok := s.Envelopes[keyID][created]; ok {
		return false, nil
	}

	// If first time, need to initialize nested map
	if _, ok := s.Envelopes[keyID]; !ok {
		s.Envelopes[keyID] = make(map[int64]*appencryption.EnvelopeKeyRecord)
	}

	s.Envelopes[keyID][created] = envelope

	return true, nil
}
