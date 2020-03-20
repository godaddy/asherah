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

func ihaveencryptedDatafrom(filename string) error {
	data, err := ioutil.ReadFile(fmt.Sprintf("%s%s", FileDirectory, filename))
	if err != nil {
		panic(err)
	}

	encryptedPayload = string(data)

	return nil
}

func idecrypttheencryptedData() error {
	crypto := aead.NewAES256GCM()
	manager, _ := kms.NewStatic(KeyManagementStaticMasterKey, crypto)
	metastore := persistence.NewSQLMetastore(connection)
	policy := appencryption.NewCryptoPolicy()
	config := &appencryption.Config{
		Service: DefaultServiceID,
		Product: DefaultProductID,
		Policy:  policy,
	}

	factory := appencryption.NewSessionFactory(config, metastore, manager, crypto)
	defer factory.Close()

	sess, err := factory.GetSession(DefaultPartitionID)
	if err != nil {
		panic(err)
	}
	defer sess.Close()

	var dataRow appencryption.DataRowRecord

	dataRowBytes, err := base64.StdEncoding.DecodeString(encryptedPayload)
	if err != nil {
		panic(err)
	}

	err2 := json.Unmarshal(dataRowBytes, &dataRow)
	if err2 != nil {
		panic(err2)
	}

	data, err := sess.Decrypt(dataRow)

	if err != nil {
		panic(err)
	}

	decryptedPayload = string(data)

	return nil
}

func ishouldgetdecryptedData() error {
	if decryptedPayload == "" {
		return errors.New("Decryption failure")
	}

	return nil
}

func decryptedDatashouldbeequalto(payload string) error {
	if decryptedPayload != payload {
		return errors.New("Payloads do not match")
	}
	return nil
}
