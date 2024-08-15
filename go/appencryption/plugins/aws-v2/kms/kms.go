package kms

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"sync"
	"time"

	"github.com/aws/aws-sdk-go-v2/service/kms"
	"github.com/aws/aws-sdk-go-v2/service/kms/types"
	"github.com/rcrowley/go-metrics"

	"github.com/godaddy/asherah/go/appencryption"
	"github.com/godaddy/asherah/go/appencryption/internal"
	"github.com/godaddy/asherah/go/appencryption/pkg/log"
)

var (
	encryptKeyTimer = metrics.GetOrRegisterTimer(appencryption.MetricsPrefix+".kms.aws.encryptkey", nil)
	decryptKeyTimer = metrics.GetOrRegisterTimer(appencryption.MetricsPrefix+".kms.aws.decryptkey", nil)
)

// AWSClient is an interface that defines the set of Amazon KMS API operations required by this package.
type AWSClient interface {
	Encrypt(ctx context.Context, params *kms.EncryptInput, optFns ...func(*kms.Options)) (*kms.EncryptOutput, error)
	Decrypt(ctx context.Context, params *kms.DecryptInput, optFns ...func(*kms.Options)) (*kms.DecryptOutput, error)
	GenerateDataKey(ctx context.Context, params *kms.GenerateDataKeyInput, optFns ...func(*kms.Options)) (*kms.GenerateDataKeyOutput, error)
}

// AWSKMS implements the KeyManagementService interface for AWS KMS using the V2 AWS SDK.
// Use the Builder to create a new AWSKMS.
//
//	keyManagementService, err := kms.NewBuilder(crypto, arnMap)
//	    .WithPreferredRegion("us-west-2")
//	    .Build()
type AWSKMS struct {
	clients []regionalClient

	crypto appencryption.AEAD
}

// NewAWS returns a new AWSKMS used for encrypting/decrypting keys with a master key.
//
// Note that this function is a convenience wrapper around the Builder and is equivalent to:
//
//	keyManagementService, err := kms.NewBuilder(crypto, arnMap)
//	    .WithPreferredRegion(region)
//	    .Build()
//
// For more advanced configuration, use the Builder directly.
func NewAWS(crypto appencryption.AEAD, preferredRegion string, arnMap map[string]string) (*AWSKMS, error) {
	return NewBuilder(crypto, arnMap).
		WithPreferredRegion(preferredRegion).
		Build()
}

// EncryptKey encrypts a byte slice in all configured regions and returns an envelope ready for storage.
func (a *AWSKMS) EncryptKey(ctx context.Context, keyBytes []byte) ([]byte, error) {
	dataKey, err := a.generateDataKey(ctx)
	if err != nil {
		return nil, err
	}

	// Wipe all bytes in plaintext after function exits
	defer internal.MemClr(dataKey.Plaintext)

	// Encrypt the key with the newly generated data key
	encKeyBytes, err := a.crypto.Encrypt(keyBytes, dataKey.Plaintext)
	if err != nil {
		return nil, fmt.Errorf("error encrypting key: %w", err)
	}

	kekEn := envelope{
		EncryptedKey: encKeyBytes,
		KEKs:         a.encryptRegionalKEKs(ctx, dataKey),
	}

	b, err := json.Marshal(kekEn)
	if err != nil {
		return nil, fmt.Errorf("error marshalling envelope: %w", err)
	}

	return b, nil
}

// generateDataKey iterates over all configured regions and generates a new data key, returning the first successful response.
// An error is returned only if all regions fail to generate a key.
func (a *AWSKMS) generateDataKey(ctx context.Context) (*kms.GenerateDataKeyOutput, error) {
	for _, c := range a.clients {
		resp, err := c.GenerateDataKey(ctx)
		if err != nil {
			log.Debugf("error generating data key in region (%s) trying next region: %s\n", c.Region, err)
			continue
		}

		return resp, nil
	}

	return nil, errors.New("all regions returned errors")
}

// encryptDataKey encrypts the generated data key in all regions and returns the results.
func (a *AWSKMS) encryptRegionalKEKs(ctx context.Context, dataKey *kms.GenerateDataKeyOutput) (out []regionalKEK) {
	ch := make(chan regionalKEK, len(a.clients))

	go a.encryptAllRegions(ctx, dataKey, ch)

	for key := range ch {
		out = append(out, key)
	}

	return out
}

// encryptAllRegions encrypts the generated data key in all regions and sends the results to the channel.
// Each region is encrypted concurrently and ch is closed when all encryption operations are complete.
func (a *AWSKMS) encryptAllRegions(ctx context.Context, dataKey *kms.GenerateDataKeyOutput, ch chan<- regionalKEK) {
	var wg sync.WaitGroup

	for _, c := range a.clients {
		// If the key is already encrypted with the master key, send it to the channel.
		if c.MasterKeyARN == *dataKey.KeyId {
			ch <- regionalKEK{
				Region:       c.Region,
				ARN:          c.MasterKeyARN,
				EncryptedKEK: dataKey.CiphertextBlob,
			}

			continue
		}

		wg.Add(1)
		go func(c regionalClient) {
			defer wg.Done()

			resp, err := c.EncryptKey(ctx, dataKey.Plaintext)
			if err != nil {
				log.Debugf("error encrypting data key in region (%s): %s\n", c.Region, err)
				return
			}

			ch <- regionalKEK{
				Region:       c.Region,
				ARN:          c.MasterKeyARN,
				EncryptedKEK: resp.CiphertextBlob,
			}
		}(c)
	}

	wg.Wait()

	close(ch) // Close the channel to signal that all encryption keys have been sent.
}

// DecryptKey decrypts the envelope and returns the decrypted key.
//
// The preferred region is used to decrypt the key if it is set, otherwise the first region is used.
// If this fails, remaining regions are tried in order.
func (a *AWSKMS) DecryptKey(ctx context.Context, data []byte) ([]byte, error) {
	var kekEn envelope

	if err := json.Unmarshal(data, &kekEn); err != nil {
		return nil, fmt.Errorf("unable to unmarshal envelope: %w", err)
	}

	// The order of the KMS KEKs contained in the envelope is not guaranteed.
	// So, we build a map of the KEKs to make it easier to look up the KEK for a given region.
	keks := make(map[string]regionalKEK, len(kekEn.KEKs))
	for _, kek := range kekEn.KEKs {
		keks[kek.Region] = kek
	}

	// Clients are ordered by the preferred region first, followed by the remaining regions.
	// So, we iterate over the clients in order and try to decrypt the key using the KEK for the region, returning on success.
	for _, c := range a.clients {
		kek, ok := keks[c.Region]
		if !ok {
			log.Debugf("no KEK found for region: %s\n", c.Region)
			continue
		}

		resp, err := c.DecryptKey(ctx, kek.EncryptedKEK)
		if err != nil {
			log.Debugf("error kms decrypt: %s\n", err)
			continue
		}

		keyBytes, err := a.crypto.Decrypt(kekEn.EncryptedKey, resp.Plaintext)
		if err != nil {
			log.Debugf("error crypto decrypt: %s\n", err)
			continue
		}

		return keyBytes, nil
	}

	return nil, errors.New("decrypt failed in all regions")
}

// PreferredRegion returns the preferred region for the AWSKMS.
func (a *AWSKMS) PreferredRegion() string {
	return a.clients[0].Region
}

// envelope contains the data required for decrypting a key.
type envelope struct {
	EncryptedKey []byte        `json:"encryptedKey"`
	KEKs         []regionalKEK `json:"kmsKeks"`
}

// regionalKEK contains an encrypted key encryption key (KEK) and its associated metadata.
type regionalKEK struct {
	Region       string `json:"region"`
	ARN          string `json:"arn"`
	EncryptedKEK []byte `json:"encryptedKek"`
}

// regionalClient contains a KMS client and region information used for
// encrypting a key in KMS in a specific region.
type regionalClient struct {
	// Client is the AWS KMS client.
	Client AWSClient

	// Region is the AWS region.
	Region string

	// ARN is the AWS KMS key ARN used for encrypting and decrypting keys.
	MasterKeyARN string
}

// GenerateDataKey generates a new data key used to encrypt a system key.
func (r *regionalClient) GenerateDataKey(ctx context.Context) (resp *kms.GenerateDataKeyOutput, err error) {
	start := time.Now()

	resp, err = r.Client.GenerateDataKey(ctx, &kms.GenerateDataKeyInput{
		KeyId:   &r.MasterKeyARN,
		KeySpec: types.DataKeySpecAes256,
	})

	generateDataKeyTimer := metrics.GetOrRegisterTimer(fmt.Sprintf("%s.kms.aws.generatedatakey.%s", appencryption.MetricsPrefix, r.Region), nil)
	generateDataKeyTimer.UpdateSince(start)

	return resp, err
}

// EncryptKey encrypts the plain-text data key using the AWS KMS client and master key ARN.
func (r *regionalClient) EncryptKey(ctx context.Context, keyBytes []byte) (resp *kms.EncryptOutput, err error) {
	defer encryptKeyTimer.UpdateSince(time.Now())

	return r.Client.Encrypt(ctx, &kms.EncryptInput{
		KeyId:     &r.MasterKeyARN,
		Plaintext: keyBytes,
	})
}

// DecryptKey decrypts the encrypted data key using the AWS KMS client and master key ARN.
func (r *regionalClient) DecryptKey(ctx context.Context, keyBytes []byte) (resp *kms.DecryptOutput, err error) {
	defer decryptKeyTimer.UpdateSince(time.Now())

	return r.Client.Decrypt(ctx, &kms.DecryptInput{
		KeyId:          &r.MasterKeyARN,
		CiphertextBlob: keyBytes,
	})
}
