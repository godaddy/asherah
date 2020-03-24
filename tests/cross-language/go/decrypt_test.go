package cltf

import (
	"encoding/base64"
	"encoding/json"
	"fmt"
	"io/ioutil"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
	"github.com/godaddy/asherah/go/appencryption/pkg/kms"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence"
	"github.com/pkg/errors"
)

var (
	decryptedPayload string
	encryptedPayload string
)

func iHaveEncryptedDataFrom(filename string) error {
	data, err := ioutil.ReadFile(fmt.Sprintf("%s%s", fileDirectory, filename))
	if err != nil {
		return err
	}

	encryptedPayload = string(data)

	return nil
}

func iDecryptTheEncryptedData() error {
	crypto := aead.NewAES256GCM()

	manager, err := kms.NewStatic(keyManagementStaticMasterKey, crypto)
	if err != nil {
		return err
	}

	metastore := persistence.NewSQLMetastore(connection)
	policy := appencryption.NewCryptoPolicy()
	config := &appencryption.Config{
		Service: defaultServiceID,
		Product: defaultProductID,
		Policy:  policy,
	}

	factory := appencryption.NewSessionFactory(config, metastore, manager, crypto)
	defer factory.Close()

	sess, err := factory.GetSession(defaultPartitionID)
	if err != nil {
		return err
	}
	defer sess.Close()

	var dataRow appencryption.DataRowRecord

	dataRowBytes, err := base64.StdEncoding.DecodeString(encryptedPayload)
	if err != nil {
		return err
	}

	err2 := json.Unmarshal(dataRowBytes, &dataRow)
	if err2 != nil {
		return err2
	}

	data, err := sess.Decrypt(dataRow)

	if err != nil {
		return err
	}

	decryptedPayload = string(data)

	return nil
}

func iShouldGetDecryptedData() error {
	if decryptedPayload == "" {
		return errors.New("decryption failure")
	}

	return nil
}

func decryptedDataShouldBeEqualTo(payload string) error {
	if decryptedPayload != payload {
		return errors.New("payloads do not match")
	}

	return nil
}
