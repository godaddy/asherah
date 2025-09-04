package main

import (
	"context"
	"errors"
	"log"
	"os"
	"regexp"
	"syscall"

	"github.com/aws/aws-lambda-go/lambda"
	"github.com/aws/aws-sdk-go-v2/config"
	"github.com/aws/aws-sdk-go-v2/service/dynamodb"
	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/pkg/crypto/aead"
	"github.com/godaddy/asherah/go/appencryption/plugins/aws-v2/dynamodb/metastore"
	awsv2kms "github.com/godaddy/asherah/go/appencryption/plugins/aws-v2/kms"
	"github.com/godaddy/asherah/go/securememory"
	"github.com/prometheus/procfs"
	"github.com/rcrowley/go-metrics"
)

type MyEvent struct {
	Name      string
	Partition string
	Payload   []byte                       `json:",omitempty"`
	DRR       *appencryption.DataRowRecord `json:",omitempty"`
}

type MyResponse struct {
	PlainText string                       `json:",omitempty"`
	DRR       *appencryption.DataRowRecord `json:",omitempty"`
	Metrics   metrics.Registry
}

var (
	invocations = metrics.GetOrRegisterCounter("asherah.samples.lambda-go.invocations", nil)
	factory     *appencryption.SessionFactory
)

func HandleRequest(ctx context.Context, event MyEvent) (*MyResponse, error) {
	log.Println("processing event:", event.Name)

	invocations.Inc(1)

	printMetrics("handlerequest.init")

	if err := initFactory(); err != nil {
		return nil, err
	}

	resp, err := tryHandle(ctx, event)
	if err != nil {
		if r, ok := err.(recoveredError); ok && r.isRetryable() {
			log.Println("recovered from panic with retryable error. retrying...")
			printMetrics("handlerequest.retry")

			if err := resetFactory(); err != nil {
				return nil, err
			}

			return tryHandle(ctx, event)
		}
	}

	return resp, err
}

func initFactory() error {
	if factory != nil {
		log.Println("factory already initialized. reusing...")
		return nil
	}

	if err := printRLimit(); err != nil {
		return err
	}

	config := &appencryption.Config{
		Service: "asherah-samples",
		Product: "lambda-sample-app",
		Policy: appencryption.NewCryptoPolicy(
			appencryption.WithSessionCache(),
			appencryption.WithSessionCacheMaxSize(10),
		),
	}

	log.Println("creating metastore")
	metastore, err := newMetastore()
	if err != nil {
		return err
	}

	crypto := aead.NewAES256GCM()

	log.Println("creating kms")
	kms, err := newKMS(crypto)
	if err != nil {
		return err
	}

	log.Println("creating session factory")
	factory = appencryption.NewSessionFactory(
		config,
		metastore,
		kms,
		crypto,
		appencryption.WithMetrics(true),
	)

	return nil
}

// printRLimit uses the getrlimit system call to retrive the current memlock resource
// limits and prints the results to the standard logger.
func printRLimit() error {
	// RLIMIT resource definitions vary by platform and has only been tested on linux/amd64
	const RLIMIT_MEMLOCK = 0x8

	var rLimit syscall.Rlimit
	if err := syscall.Getrlimit(RLIMIT_MEMLOCK, &rLimit); err != nil {
		return err
	}

	log.Printf("MEMLOCK RLIMIT = %d:%d", rLimit.Cur, rLimit.Max)

	return nil
}

func resetFactory() error {
	// reset factory
	factory.Close()
	factory = nil

	// reset metrics
	securememory.AllocCounter.Clear()
	securememory.InUseCounter.Clear()

	return initFactory()
}

// newMetastore returns a newly initialized DynamoDB metastore using AWS SDK v2.
func newMetastore() (*metastore.Metastore, error) {
	awsCfg, err := config.LoadDefaultConfig(context.TODO(),
		config.WithRegion(os.Getenv("AWS_REGION")),
	)
	if err != nil {
		return nil, err
	}

	client := dynamodb.NewFromConfig(awsCfg)
	// Note: X-Ray tracing for AWS SDK v2 requires different setup
	// xray.AWS(client) // This would need to be updated for v2 X-Ray integration

	metastoreOpts := []metastore.Option{
		metastore.WithDynamoDBClient(client),
	}

	if tableName := os.Getenv("ASHERAH_METASTORE_TABLE_NAME"); tableName != "" {
		metastoreOpts = append(metastoreOpts, metastore.WithTableName(tableName))
	}

	return metastore.NewDynamoDB(metastoreOpts...)
}

// newKMS returns a newly initialized AWS KMS using AWS SDK v2.
func newKMS(crypto appencryption.AEAD) (*awsv2kms.AWSKMS, error) {
	region := os.Getenv("AWS_REGION")

	// build the ARN regions including preferred region
	regionArnMap := map[string]string{
		region: os.Getenv("ASHERAH_KMS_KEY_ARN"),
	}

	awsCfg, err := config.LoadDefaultConfig(context.TODO(),
		config.WithRegion(region),
	)
	if err != nil {
		return nil, err
	}

	k, err := awsv2kms.NewBuilder(crypto, regionArnMap).
		WithAWSConfig(awsCfg).
		WithPreferredRegion(region).
		Build()
	if err != nil {
		return nil, err
	}

	// Note: X-Ray tracing for AWS SDK v2 requires different setup
	// X-Ray integration would need to be updated for v2 clients

	return k, nil
}

// recoveredError is a wrapper for errors recovered during a panic.
type recoveredError struct{ error }

func (e recoveredError) isRetryable() bool {
	// The underlying memcall library that wraps the mlock syscall returns a basic errorString (à la errors.New()),
	// which means, unfortunately, we must restort to inspecting err.Error() to ensure we have a supported error "type".

	pattern := `^\<memcall\> could not acquire lock on 0x[0-9a-f].*?, limit reached\? \[Err: cannot allocate memory\]`
	mlockErr := regexp.MustCompile(pattern)

	return mlockErr.MatchString(e.Error())
}

func tryHandle(ctx context.Context, event MyEvent) (resp *MyResponse, err error) {
	defer func() {
		if r := recover(); r != nil {
			if e, ok := r.(error); ok {
				err = recoveredError{e}
			} else {
				panic(r)
			}
		}
	}()

	switch {
	case len(event.Payload) > 0:
		return handleEncrypt(ctx, event)
	case event.DRR != nil:
		return handleDecrypt(ctx, event)
	default:
		return nil, errors.New("event must contain a Payload (for encryption) or DRR (for decryption)")
	}
}

func handleEncrypt(ctx context.Context, event MyEvent) (*MyResponse, error) {
	log.Println("handling encrypt for", event.Name)
	printMetrics("encrypt.getsession")

	session, err := factory.GetSession(event.Partition)
	if err != nil {
		return nil, err
	}
	defer func() {
		printMetrics("encrypt.close")
		session.Close()
	}()

	printMetrics("encrypt.encryptcontext")
	encData, err := session.Encrypt(ctx, event.Payload)
	if err != nil {
		return nil, err
	}

	return &MyResponse{
		DRR:     encData,
		Metrics: metrics.DefaultRegistry,
	}, nil
}

func handleDecrypt(ctx context.Context, event MyEvent) (*MyResponse, error) {
	log.Printf("handling decrypt for %s\n", event.Name)
	printMetrics("decrypt.getsession")

	session, err := factory.GetSession(event.Partition)
	if err != nil {
		return nil, err
	}
	defer func() {
		printMetrics("decrypt.close")
		session.Close()
	}()

	printMetrics("decrypt.decryptcontext")
	plaintext, err := session.Decrypt(ctx, *event.DRR)
	if err != nil {
		return nil, err
	}

	return &MyResponse{
		PlainText: string(plaintext),
		Metrics:   metrics.DefaultRegistry,
	}, nil
}

func printMetrics(msg string) {
	fs, err := procfs.NewDefaultFS()
	if err != nil {
		panic("could not get default FS")
	}

	minfo, err := fs.Meminfo()
	if err != nil {
		panic("could not get process meminfo")
	}

	log.Printf("metrics: %s, secret.allocations: %d, secret.inuse: %d, locked.memory: %d",
		msg,
		securememory.AllocCounter.Count(),
		securememory.InUseCounter.Count(),
		minfo.Mlocked,
	)
}

func main() {
	lambda.Start(HandleRequest)
}
