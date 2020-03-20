package cltf

import (
	"database/sql"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"os"

	"github.com/cucumber/godog"
	"github.com/go-sql-driver/mysql"
	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
	"github.com/godaddy/asherah/go/appencryption/pkg/kms"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence"
	"github.com/pkg/errors"
)

var (
	payloadString          string
	encryptedPayloadString string
	connection             *sql.DB
)

func iHave(payload string) error {
	payloadString = payload

	return nil
}

func iEncryptTheData() error {
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

	dataRow, e := sess.Encrypt([]byte(payloadString))
	if e != nil {
		panic(e)
	}

	encryptedData, _ := json.Marshal(dataRow)
	encryptedPayloadString = base64.StdEncoding.EncodeToString(encryptedData)

	return nil
}

func ishouldgetencryptedData() error {
	var filePath = fmt.Sprintf("%s%s", FileDirectory, FileName)

	if _, err := os.Stat(filePath); err == nil {
		os.Remove(filePath)
	}

	f, err := os.Create(filePath)
	if err != nil {
		panic(err)
	}
	defer f.Close()

	f.WriteString(encryptedPayloadString)

	return nil
}

func encryptedDatashouldnotbeequaltodata() error {
	if payloadString == encryptedPayloadString {
		return errors.New("Encryption failure")
	}

	return nil
}

// nolint: deadcode
func FeatureContext(s *godog.Suite) {
	s.BeforeSuite(func() {
		connectSQL()
	})
	// Encrypt feature steps
	s.Step(`^I have "([^"]*)"$`, iHave)
	s.Step(`^I encrypt the data$`, iEncryptTheData)
	s.Step(`^I should get encrypted_data$`, ishouldgetencryptedData)
	s.Step(`^encrypted_data should not be equal to data$`, encryptedDatashouldnotbeequaltodata)

	// Decrypt feature steps
	s.Step(`^I have encrypted_data from "([^"]*)"$`, ihaveencryptedDatafrom)
	s.Step(`^I decrypt the encrypted_data$`, idecrypttheencryptedData)
	s.Step(`^I should get decrypted_data$`, ishouldgetdecryptedData)
	s.Step(`^decrypted_data should be equal to "([^"]*)"$`, decryptedDatashouldbeequalto)
}

// connectSQL connects to the mysql instance with the provided connection string.
func connectSQL() error {
	dsn, err := mysql.ParseDSN(ConnectionString)
	if err != nil {
		return err
	}

	dsn.ParseTime = true

	conn, err := sql.Open("mysql", dsn.FormatDSN())
	if err != nil {
		return err
	}

	connection = conn

	return nil
}
