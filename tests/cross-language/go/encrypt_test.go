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

	dataRow, err := sess.Encrypt([]byte(payloadString))
	if err != nil {
		return err
	}

	encryptedData, err := json.Marshal(dataRow)
	if err != nil {
		return err
	}

	encryptedPayloadString = base64.StdEncoding.EncodeToString(encryptedData)

	return nil
}

func iShouldGetEncryptedData() error {
	var filePath = fmt.Sprintf("%s%s", fileDirectory, fileName)

	if _, err := os.Stat(filePath); err == nil {
		os.Remove(filePath)
	}

	f, err := os.Create(filePath)
	if err != nil {
		return err
	}
	defer f.Close()

	f.WriteString(encryptedPayloadString)

	return nil
}

func encryptedDataShouldNotBeEqualToData() error {
	if payloadString == encryptedPayloadString {
		return errors.New("encryption failure")
	}

	return nil
}

func FeatureContext(s *godog.Suite) {
	s.BeforeSuite(func() {
		connectSQL()
	})
	// Encrypt feature steps
	s.Step(`^I have "([^"]*)"$`, iHave)
	s.Step(`^I encrypt the data$`, iEncryptTheData)
	s.Step(`^I should get encrypted_data$`, iShouldGetEncryptedData)
	s.Step(`^encrypted_data should not be equal to data$`, encryptedDataShouldNotBeEqualToData)

	// Decrypt feature steps
	s.Step(`^I have encrypted_data from "([^"]*)"$`, iHaveEncryptedDataFrom)
	s.Step(`^I decrypt the encrypted_data$`, iDecryptTheEncryptedData)
	s.Step(`^I should get decrypted_data$`, iShouldGetDecryptedData)
	s.Step(`^decrypted_data should be equal to "([^"]*)"$`, decryptedDataShouldBeEqualTo)
}

// connectionString returns the RDBMS connection string
func connectionString() string {
	return fmt.Sprintf(
		"%s:%s@tcp(localhost:3306)/%s",
		os.Getenv("TEST_DB_USER"),
		os.Getenv("TEST_DB_PASSWORD"),
		os.Getenv("TEST_DB_NAME"))
}

// connectSQL connects to the mysql instance with the provided connection string.
func connectSQL() error {
	dsn, err := mysql.ParseDSN(connectionString())
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
