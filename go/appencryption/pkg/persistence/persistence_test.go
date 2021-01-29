package persistence_test

import (
	"context"
	"encoding/json"
	"fmt"
	"strconv"
	"testing"

	"github.com/google/uuid"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
	"github.com/godaddy/asherah/go/appencryption/pkg/kms"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence"
)

var payloads = [][]byte{
	[]byte("TestString"),
	[]byte("ᐊᓕᒍᖅ ᓂᕆᔭᕌᖓᒃᑯ ᓱᕋᙱᑦᑐᓐᓇᖅᑐᖓ "),
	[]byte("床前明月光，疑是地上霜。举头望明月，低头思故乡。"),
}

type testStore map[uuid.UUID][]byte

func (s testStore) Store(_ context.Context, d appencryption.DataRowRecord) (interface{}, error) {
	b, err := json.Marshal(d)
	if err != nil {
		return nil, err
	}

	key := uuid.New()
	s[key] = b

	return key, nil
}

func (s testStore) Load(_ context.Context, key interface{}) (*appencryption.DataRowRecord, error) {
	var d appencryption.DataRowRecord

	data, ok := s[key.(uuid.UUID)]
	if !ok {
		return nil, fmt.Errorf("could not load value for key %s", key)
	}

	err := json.Unmarshal(data, &d)

	return &d, err
}

func TestPersistence(t *testing.T) {
	factory := newSessionFactory()
	defer factory.Close()

	sess, err := factory.GetSession("some session")
	require.NoError(t, err)

	defer sess.Close()

	store := make(testStore)

	for _, payload := range payloads {
		key, err := sess.Store(context.Background(), payload, store)
		require.NoError(t, err)

		loaded, err := sess.Load(context.Background(), key, store)
		require.NoError(t, err)
		assert.Equal(t, payload, loaded)
	}
}

func TestPersistenceFuncs(t *testing.T) {
	factory := newSessionFactory()
	defer factory.Close()

	sess, err := factory.GetSession("test-partition")
	require.NoError(t, err)

	defer sess.Close()

	store := make(map[string]appencryption.DataRowRecord)

	for i, payload := range payloads {
		i := i
		persistenceKey, err := sess.Store(
			context.Background(),
			payload,
			persistence.StorerFunc(func(_ context.Context, d appencryption.DataRowRecord) (interface{}, error) {
				key := strconv.Itoa(i)
				store[key] = d
				return key, nil
			}),
		)
		require.NoError(t, err)
		assert.Equal(t, strconv.Itoa(i), persistenceKey)
	}

	assert.Equal(t, len(payloads), len(store), "exptected store to contain one element for each payload")

	for i, payload := range payloads {
		loaded, err := sess.Load(
			context.Background(),
			strconv.Itoa(i),
			persistence.LoaderFunc(func(_ context.Context, key interface{}) (*appencryption.DataRowRecord, error) {
				d := store[key.(string)]
				return &d, nil
			}),
		)
		require.NoError(t, err)
		assert.Equal(t, payload, loaded)
	}
}

func newSessionFactory() *appencryption.SessionFactory {
	crypto := aead.NewAES256GCM()
	config := &appencryption.Config{
		Service: "persistence test",
		Product: "testing",
		Policy:  appencryption.NewCryptoPolicy(),
	}
	metastore := persistence.NewMemoryMetastore()

	key, err := kms.NewStatic("thisIsAStaticMasterKeyForTesting", crypto)
	if err != nil {
		panic(err)
	}

	return appencryption.NewSessionFactory(config, metastore, key, crypto)
}
