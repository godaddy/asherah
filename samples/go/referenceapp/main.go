package main

import (
	"bytes"
	"database/sql"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"strings"

	"github.com/aws/aws-sdk-go/aws"
	"github.com/aws/aws-sdk-go/aws/session"
	"github.com/go-sql-driver/mysql"
	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
	"github.com/godaddy/asherah/go/appencryption/pkg/kms"
	"github.com/godaddy/asherah/go/appencryption/pkg/persistence"
	"github.com/google/logger"
	"github.com/jessevdk/go-flags"
	"github.com/pkg/errors"
)

var (
	connection *sql.DB
	opts       Options
)

type Options struct {
	Drr              string `short:"d" long:"drr-to-decrypt" description:"DRR to be decrypted"`
	Payload          string `short:"p" long:"payload-to-encrypt" description:"payload to be encrypted"`
	Metastore        string `short:"m" long:"metastore" description:"Configure what metastore to use (DYNAMODB/SQL/MEMORY)"`
	ConnectionString string `short:"c" long:"conn" description:"MySQL connection String"`
	KmsType          string `long:"kms-type" description:"Type of key management service to use (AWS/STATIC)"`
	PreferredRegion  string `long:"preferred-region" description:"Preferred region to use for KMS if using AWS KMS. Required for AWS KMS."`
	RegionTuples     string `long:"region-arn-tuples" description:"Comma separated list of <region>=<kms_arn> tuples. Required for AWS KMS."`
}

// connectSQL connects to the mysql instance with the provided connection string.
func connectSQL() error {
	dsn, err := mysql.ParseDSN(opts.ConnectionString)
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

func createMetastore() appencryption.Metastore {
	switch opts.Metastore {
	case "RDBMS":
		logger.Info("using sql metastore")

		if opts.ConnectionString == "" {
			panic(errors.Errorf("connection string is mandatory with  Metastore Type: SQL"))
		}

		if err := connectSQL(); err != nil {
			panic(err)
		}

		return persistence.NewSQLMetastore(connection)
	case "DYNAMODB":
		logger.Info("using dynamodb metastore")

		sess, e := session.NewSession(&aws.Config{
			Region: aws.String("us-west-2"),
			//Uncomment to use local dynamodb
			//Endpoint: aws.String("http://localhost:8000"),
		})

		if e != nil {
			panic(e)
		}

		return persistence.NewDynamoDBMetastore(sess)
	default:
		logger.Info("using in-memory metastore")
		return persistence.NewMemoryMetastore()
	}
}

func createKMS(crypto appencryption.AEAD) (appencryption.KeyManagementService, error) {
	switch opts.KmsType {
	case "AWS":
		logger.Info("using aws kms")

		// build the ARN regions including preferred region
		if opts.PreferredRegion == "" || opts.RegionTuples == "" {
			panic(errors.Errorf("preferred region and <region>=<arn> tuples are mandatory with  KMS Type: AWS"))
		}

		regionArnMap := make(map[string]string)

		splits := strings.Split(opts.RegionTuples, ",")
		for _, regionArn := range splits {
			regionArnValue := strings.Split(regionArn, "=")
			regionArnMap[regionArnValue[0]] = regionArnValue[1]
		}

		return kms.NewAWS(crypto, opts.PreferredRegion, regionArnMap)
	default:
		logger.Info("using static kms")
		return kms.NewStatic("test_master_key_that_is_32_bytes", crypto)
	}
}

func main() {
	logger.Init("Default", true, false, new(bytes.Buffer))

	_, err := flags.Parse(&opts)
	if err != nil {
		if e, ok := err.(*flags.Error); ok && e.Type == flags.ErrHelp {
			return
		}

		panic(err)
	}

	if opts.Payload != "" && opts.Drr != "" {
		panic(errors.Errorf("either payload or drr can be provided"))
	}

	policy := appencryption.NewCryptoPolicy()
	crypto := aead.NewAES256GCM()

	manager, err := createKMS(crypto)
	if err != nil {
		panic(err)
	}

	metastore := createMetastore()
	config := &appencryption.Config{
		Service: "reference_app",
		Product: "productId",
		Policy:  policy,
	}

	// Create a session factory for this app. Normally this would be done upon app startup and the
	// same factory would be used anytime a new session is needed for a partition (e.g., shopper).
	factory := appencryption.NewSessionFactory(config, metastore, manager, crypto)
	// The factory should be closed when done using it.
	defer factory.Close()

	// Now create an actual session for a partition (which in our case is a pretend shopper id). This session is used
	// for a transaction and needs to be closed after use.
	sess, err := factory.GetSession("shopper123")
	if err != nil {
		panic(err)
	}

	// Close the session when done using it.
	defer sess.Close()

	// If we get a payload as a command line argument, use that
	var payload string
	if opts.Payload != "" {
		payload = opts.Payload
	} else {
		payload = "mysupersecretpayload"
	}

	// If we get a DRR as a command line argument, we want to directly decrypt it
	var dataRowString string
	if opts.Drr != "" {
		dataRowString = opts.Drr
	} else {
		// Encrypt the payload
		dataRow, e := sess.Encrypt([]byte(payload))
		if e != nil {
			panic(e)
		}

		// Consider this us "persisting" the DRR
		// nolint: errcheck
		b, _ := json.Marshal(dataRow)
		dataRowString = base64.StdEncoding.EncodeToString(b)
		fmt.Println("\ndata row record as string:", dataRowString)
	}

	var dataRow appencryption.DataRowRecord
	// nolint: errcheck
	dataRowBytes, _ := base64.StdEncoding.DecodeString(dataRowString)
	// nolint: errcheck
	_ = json.Unmarshal(dataRowBytes, &dataRow)

	// Decrypt the payload
	data, err := sess.Decrypt(dataRow)
	if err != nil {
		panic(err)
	}

	fmt.Println("\ndecrypted value =", string(data)) // Will output the payload

	if opts.Drr == "" {
		fmt.Println("\nmatches =", string(data) == payload)
	}
}
