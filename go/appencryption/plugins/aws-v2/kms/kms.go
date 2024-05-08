package kms

import (
	"context"
	"encoding/json"
	"fmt"
	"sort"
	"sync"
	"time"

	"github.com/aws/aws-sdk-go-v2/aws"
	"github.com/aws/aws-sdk-go-v2/config"
	"github.com/aws/aws-sdk-go-v2/service/kms"
	"github.com/aws/aws-sdk-go-v2/service/kms/types"
	"github.com/pkg/errors"
	"github.com/rcrowley/go-metrics"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/internal"
	"github.com/godaddy/asherah/go/appencryption/pkg/log"
)

// KMS Metrics Counters & Timers
var (
	_ appencryption.KeyManagementService = (*AWSKMS)(nil)

	clientFactory = kms.NewFromConfig

	generateDataKeyFunc   = generateDataKey
	encryptAllRegionsFunc = encryptAllRegions

	encryptKeyTimer = metrics.GetOrRegisterTimer(fmt.Sprintf("%s.kms.aws.encryptkey", appencryption.MetricsPrefix), nil)
	decryptKeyTimer = metrics.GetOrRegisterTimer(fmt.Sprintf("%s.kms.aws.decryptkey", appencryption.MetricsPrefix), nil)
)

// KMS is implemented by the client in the kms package from the AWS SDK.
// We only use a subset of methods defined below.
type KMS interface {
	Encrypt(context.Context, *kms.EncryptInput, ...func(*kms.Options)) (*kms.EncryptOutput, error)
	GenerateDataKey(context.Context, *kms.GenerateDataKeyInput, ...func(*kms.Options)) (*kms.GenerateDataKeyOutput, error)
	Decrypt(context.Context, *kms.DecryptInput, ...func(*kms.Options)) (*kms.DecryptOutput, error)
}

// AWSKMSClient contains a KMS client and region information used for
// encrypting a key in KMS.
type AWSKMSClient struct {
	KMS    KMS
	Region string
	ARN    string
}

// newAWSKMSClient returns a new AWSKMSClient struct with a new KMS client.
func newAWSKMSClient(cfg aws.Config, region, arn string) AWSKMSClient {
	return AWSKMSClient{
		KMS:    clientFactory(cfg, func(o *kms.Options) { o.Region = region }),
		Region: region,
		ARN:    arn,
	}
}

// createAWSKMSClients creates a client for each region in the arn map.
func createAWSKMSClients(arnMap map[string]string) ([]AWSKMSClient, error) {
	cfg, err := config.LoadDefaultConfig(context.Background())
	if err != nil {
		return nil, errors.Wrap(err, "unable to create new config")
	}

	clients := make([]AWSKMSClient, 0)

	for region, arn := range arnMap {
		clients = append(clients, newAWSKMSClient(cfg, region, arn))
	}

	return clients, nil
}

// AWSKMS implements the KeyManagementService interface and handles
// encryption/decryption in KMS.
type AWSKMS struct {
	Crypto   appencryption.AEAD
	Clients  []AWSKMSClient
	Registry metrics.Registry
}

func sortClients(preferredRegion string, clients []AWSKMSClient) []AWSKMSClient {
	sort.SliceStable(clients, func(i, _ int) bool {
		return clients[i].Region == preferredRegion
	})

	return clients
}

// NewAWS returns a new AWSKMS used for encrypting/decrypting
// keys with a master key.
func NewAWS(crypto appencryption.AEAD, preferredRegion string, arnMap map[string]string) (*AWSKMS, error) {
	return newAWS(crypto, preferredRegion, awsARNMap(arnMap))
}

type awsARNMap map[string]string

func (a awsARNMap) createAWSKMSClients() ([]AWSKMSClient, error) {
	return createAWSKMSClients(a)
}

type clientMapper interface {
	createAWSKMSClients() ([]AWSKMSClient, error)
}

func newAWS(crypto appencryption.AEAD, preferredRegion string, arnMap clientMapper) (*AWSKMS, error) {
	clients, err := arnMap.createAWSKMSClients()
	if err != nil {
		return nil, err
	}

	return &AWSKMS{
		Crypto:  crypto,
		Clients: sortClients(preferredRegion, clients),
	}, nil
}

// envelope contains the data required for decrypting a key.
// This data is base64 encoded and returned in the key_record
// field.
type envelope struct {
	EncryptedKey []byte `json:"encryptedKey"`
	KMSKEKs      keys   `json:"kmsKeks"`
}

// keys contains the encrypted text from each region supported.
type keys []encryptionKey

// get retrieves an encryption key for the provided
// region. Returns nil if missing.
func (k keys) get(region string) *encryptionKey {
	for i := range k {
		if k[i].Region == region {
			return &k[i]
		}
	}

	return nil
}

// encryptionKey contains the region and ARN of the key that was used
// to encrypt. This allows multi-region decryption.
type encryptionKey struct {
	Region       string `json:"region"`
	ARN          string `json:"arn"`
	EncryptedKEK []byte `json:"encryptedKek"`
}

// EncryptKey encrypts a byte slice in all supported regions and returns an envelope ready
// to store in metastore.
func (m *AWSKMS) EncryptKey(ctx context.Context, keyBytes []byte) ([]byte, error) {
	dataKey, err := generateDataKeyFunc(ctx, m.Clients)
	if err != nil {
		return nil, err
	}

	// Wipe all bytes in plaintext after function exits
	defer internal.MemClr(dataKey.Plaintext)

	encKeyBytes, err := m.Crypto.Encrypt(keyBytes, dataKey.Plaintext)

	if err != nil {
		return nil, err
	}

	kekEn := envelope{
		EncryptedKey: encKeyBytes,
		KMSKEKs:      make(keys, 0),
	}

	for k := range encryptAllRegionsFunc(ctx, dataKey, m.Clients) {
		kekEn.KMSKEKs = append(kekEn.KMSKEKs, k)
	}

	b, err := json.Marshal(kekEn)
	if err != nil {
		return nil, err
	}

	return b, nil
}

// encryptAllRegions encrypts the plain-text key using the KMS keys of all requested reegions
func encryptAllRegions(ctx context.Context, resp *kms.GenerateDataKeyOutput, clients []AWSKMSClient) <-chan encryptionKey {
	var wg sync.WaitGroup

	results := make(chan encryptionKey, len(clients))

	for i := range clients {
		c := &clients[i]
		if c.ARN == *resp.KeyId {
			results <- encryptionKey{
				Region:       c.Region,
				ARN:          c.ARN,
				EncryptedKEK: resp.CiphertextBlob,
			}
		} else {
			wg.Add(1)
			go func(c *AWSKMSClient) {
				defer wg.Done()

				defer encryptKeyTimer.UpdateSince(time.Now())

				encResp, err := c.KMS.Encrypt(ctx, &kms.EncryptInput{
					KeyId:     aws.String(c.ARN),
					Plaintext: resp.Plaintext,
				})

				if err != nil {
					return
				}

				results <- encryptionKey{
					Region:       c.Region,
					ARN:          c.ARN,
					EncryptedKEK: encResp.CiphertextBlob,
				}
			}(c)
		}
	}

	go func() {
		defer close(results)

		wg.Wait()
	}()

	return results
}

// generateDataKey generates a new data key used to encrypt a key.
// The first successful response is used. An error is returned only
// if all regions fail to generate a key.
func generateDataKey(ctx context.Context, clients []AWSKMSClient) (*kms.GenerateDataKeyOutput, error) {
	for i := range clients {
		c := &clients[i]

		start := time.Now()

		resp, err := c.KMS.GenerateDataKey(ctx, &kms.GenerateDataKeyInput{
			KeyId:   &c.ARN,
			KeySpec: types.DataKeySpecAes256,
		})

		generateDataKeyTimer := metrics.GetOrRegisterTimer(fmt.Sprintf("%s.kms.aws.generatedatakey.%s", appencryption.MetricsPrefix, c.Region), nil)
		generateDataKeyTimer.UpdateSince(start)

		if err != nil {
			log.Debugf("error generating data key in region (%s) trying next region: %s\n", c.Region, err)
			continue
		}

		return resp, nil
	}

	return nil, errors.New("all regions returned errors")
}

// DecryptKey decrypts an encrypted byte slice and returns the unencrypted key. The preferred region provided in the
// config is tried first, if this fails the remaining regions are tried.
func (m *AWSKMS) DecryptKey(ctx context.Context, keyBytes []byte) ([]byte, error) {
	var en envelope

	if err := json.Unmarshal(keyBytes, &en); err != nil {
		return nil, errors.Wrap(err, "unable to unmarshal envelope")
	}

	for i := range m.Clients {
		c := &m.Clients[i]
		if key := en.KMSKEKs.get(c.Region); key != nil {
			start := time.Now()

			output, err := c.KMS.Decrypt(ctx, &kms.DecryptInput{
				CiphertextBlob: key.EncryptedKEK,
			})

			decryptKeyTimer.UpdateSince(start)

			if err != nil {
				log.Debugf("error kms decrypt: %s\n", err)
				continue
			}

			decryptedKeyBytes, err := m.Crypto.Decrypt(en.EncryptedKey, output.Plaintext)
			if err != nil {
				log.Debugf("error crypto decrypt: %s\n", err)
				continue
			}

			return decryptedKeyBytes, nil
		}
	}

	return nil, errors.New("decrypt failed in all regions")
}
