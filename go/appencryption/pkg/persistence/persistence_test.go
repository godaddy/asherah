package persistence_test

import (
	"context"
	"encoding/json"
	"strconv"
	"testing"

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

type testStore map[string][]byte

func (s testStore) Store(_ context.Context, key string, d appencryption.DataRowRecord) error {
	b, err := json.Marshal(d)
	if err != nil {
		return err
	}

	s[key] = b

	return nil
}

func (s testStore) Load(_ context.Context, key string) (*appencryption.DataRowRecord, error) {
	var d appencryption.DataRowRecord
	err := json.Unmarshal(s[key], &d)

	return &d, err
}

func TestPersistence(t *testing.T) {
	factory := newSessionFactory()
	defer factory.Close()

	sess, err := factory.GetSession("some session")
	require.NoError(t, err)

	defer sess.Close()

	store := make(testStore)

	for i, payload := range payloads {
		key := strconv.Itoa(i)

		require.NoError(t, sess.Store(key, payload, store))

		loaded, err := sess.Load(key, store)
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
		err := sess.Store(
			strconv.Itoa(i),
			payload,
			persistence.StorerFunc(func(_ context.Context, key string, d appencryption.DataRowRecord) error {
				store[key] = d
				return nil
			}),
		)
		require.NoError(t, err)
	}

	assert.Equal(t, len(payloads), len(store), "exptected store to contain one element for each payload")

	for i, payload := range payloads {
		loaded, err := sess.Load(
			strconv.Itoa(i),
			persistence.LoaderFunc(func(_ context.Context, key string) (*appencryption.DataRowRecord, error) {
				d := store[key]
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
