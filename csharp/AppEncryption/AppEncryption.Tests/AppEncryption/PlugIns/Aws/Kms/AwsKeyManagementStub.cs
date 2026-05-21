using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Amazon.Runtime;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.PlugIns.Aws.Kms
{
    /// <summary>
    /// Stub implementation of IAmazonKeyManagementService for testing purposes.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="AwsKeyManagementStub"/> class.
    /// </remarks>
    /// <param name="keyArn">The key ARN for this stub.</param>
    [ExcludeFromCodeCoverage]
    public class AwsKeyManagementStub(string keyArn) : IAmazonKeyManagementService
    {
        public IClientConfig Config => throw new NotImplementedException();
        public IKeyManagementServicePaginatorFactory Paginators => throw new NotImplementedException();

        public Task<CancelKeyDeletionResponse> CancelKeyDeletionAsync(CancelKeyDeletionRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<CancelKeyDeletionResponse> CancelKeyDeletionAsync(string keyId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ConnectCustomKeyStoreResponse> ConnectCustomKeyStoreAsync(ConnectCustomKeyStoreRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<CreateAliasResponse> CreateAliasAsync(CreateAliasRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<CreateAliasResponse> CreateAliasAsync(string aliasName, string targetKeyId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<CreateCustomKeyStoreResponse> CreateCustomKeyStoreAsync(CreateCustomKeyStoreRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<CreateGrantResponse> CreateGrantAsync(CreateGrantRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<CreateKeyResponse> CreateKeyAsync(CreateKeyRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DecryptResponse> DecryptAsync(DecryptRequest request, CancellationToken cancellationToken = default)
        {
            // Read the ciphertext from the request using GetBuffer() like the real implementations
            byte[] ciphertext;
            using (var stream = request.CiphertextBlob)
            {
                ciphertext = new byte[stream.Length];
                stream.ReadExactly(ciphertext, 0, ciphertext.Length);
            }

            var keyBytes = System.Text.Encoding.UTF8.GetBytes(keyArn);

            // Verify that the first bytes match the _keyBytes
            if (ciphertext.Length < keyBytes.Length)
            {
                throw new AmazonServiceException($"Ciphertext too short. Expected at least {keyBytes.Length} bytes, got {ciphertext.Length}");
            }

            for (var i = 0; i < keyBytes.Length; i++)
            {
                if (ciphertext[i] != keyBytes[i])
                {
                    throw new AmazonServiceException($"Ciphertext key bytes don't match expected _keyBytes at position {i}");
                }
            }

            // Remove the _keyBytes from the beginning of the ciphertext
            var plaintext = new byte[ciphertext.Length - keyBytes.Length];
            Array.Copy(ciphertext, keyBytes.Length, plaintext, 0, plaintext.Length);

            // Simply copy the modified bytes to plaintext
            var response = new DecryptResponse
            {
                Plaintext = new MemoryStream(plaintext, 0, plaintext.Length, false, true),
            };

            return Task.FromResult(response);
        }

        public Task<DeleteAliasResponse> DeleteAliasAsync(DeleteAliasRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteAliasResponse> DeleteAliasAsync(string aliasName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteCustomKeyStoreResponse> DeleteCustomKeyStoreAsync(DeleteCustomKeyStoreRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteImportedKeyMaterialResponse> DeleteImportedKeyMaterialAsync(DeleteImportedKeyMaterialRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DeriveSharedSecretResponse> DeriveSharedSecretAsync(DeriveSharedSecretRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DescribeCustomKeyStoresResponse> DescribeCustomKeyStoresAsync(DescribeCustomKeyStoresRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DescribeKeyResponse> DescribeKeyAsync(DescribeKeyRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DescribeKeyResponse> DescribeKeyAsync(string keyId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DisableKeyResponse> DisableKeyAsync(DisableKeyRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DisableKeyResponse> DisableKeyAsync(string keyId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DisableKeyRotationResponse> DisableKeyRotationAsync(DisableKeyRotationRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DisableKeyRotationResponse> DisableKeyRotationAsync(string keyId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DisconnectCustomKeyStoreResponse> DisconnectCustomKeyStoreAsync(DisconnectCustomKeyStoreRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<EnableKeyResponse> EnableKeyAsync(EnableKeyRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<EnableKeyResponse> EnableKeyAsync(string keyId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<EnableKeyRotationResponse> EnableKeyRotationAsync(EnableKeyRotationRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<EnableKeyRotationResponse> EnableKeyRotationAsync(string keyId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<EncryptResponse> EncryptAsync(EncryptRequest request, CancellationToken cancellationToken = default)
        {
            // Validate the KeyId matches our stored KeyArn
            if (request.KeyId != keyArn)
            {
                throw new ArgumentException($"KeyId '{request.KeyId}' does not match expected KeyArn '{keyArn}'");
            }

            // Read the plaintext from the request
            byte[] plaintext;
            using (var stream = request.Plaintext)
            {
                plaintext = new byte[stream.Length];
                stream.ReadExactly(plaintext, 0, plaintext.Length);
            }

            var keyBytes = System.Text.Encoding.UTF8.GetBytes(keyArn);

            // Prepend the _keyBytes to the beginning of the plaintext
            var ciphertext = new byte[keyBytes.Length + plaintext.Length];
            Array.Copy(keyBytes, 0, ciphertext, 0, keyBytes.Length);
            Array.Copy(plaintext, 0, ciphertext, keyBytes.Length, plaintext.Length);

            // Simply copy the modified bytes to ciphertext blob
            var response = new EncryptResponse
            {
                CiphertextBlob = new MemoryStream(ciphertext, 0, ciphertext.Length, true, true),
                KeyId = keyArn
            };
            return Task.FromResult(response);
        }

        public Task<GenerateDataKeyResponse> GenerateDataKeyAsync(GenerateDataKeyRequest request, CancellationToken cancellationToken = default)
        {
            // Validate the KeyId matches our stored KeyArn
            if (request.KeyId != keyArn)
            {
                throw new ArgumentException($"KeyId '{request.KeyId}' does not match expected KeyArn '{keyArn}'");
            }

            // Simulated error from KMS
            if (keyArn == "ERROR")
            {
                throw new KeyUnavailableException("Simulated KMS error for testing purposes");
            }

            // Generate fake data based on the _keyArn to make it unique per ARN
            var fakePlaintext = GenerateFakeDataFromArn(keyArn, "plaintext");
            var fakeCiphertext = GenerateFakeDataFromArn(keyArn, "ciphertext");

            var response = new GenerateDataKeyResponse
            {
                Plaintext = new MemoryStream(fakePlaintext, 0, fakePlaintext.Length, true, true),
                CiphertextBlob = new MemoryStream(fakeCiphertext, 0, fakeCiphertext.Length, true, true),
                KeyId = keyArn
            };
            return Task.FromResult(response);
        }

        private static byte[] GenerateFakeDataFromArn(string keyArn, string suffix)
        {
            // Create deterministic fake data based on the ARN and suffix
            var input = $"{keyArn}-{suffix}";
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));

            // Take first 32 bytes for AES-256 key size
            var result = new byte[32];
            Array.Copy(hash, result, Math.Min(hash.Length, result.Length));
            return result;
        }

        public Task<GenerateDataKeyPairResponse> GenerateDataKeyPairAsync(GenerateDataKeyPairRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<GenerateDataKeyPairWithoutPlaintextResponse> GenerateDataKeyPairWithoutPlaintextAsync(GenerateDataKeyPairWithoutPlaintextRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<GenerateDataKeyWithoutPlaintextResponse> GenerateDataKeyWithoutPlaintextAsync(GenerateDataKeyWithoutPlaintextRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<GenerateMacResponse> GenerateMacAsync(GenerateMacRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<GenerateRandomResponse> GenerateRandomAsync(GenerateRandomRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<GenerateRandomResponse> GenerateRandomAsync(int? numberOfBytes, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<GetKeyPolicyResponse> GetKeyPolicyAsync(GetKeyPolicyRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<GetKeyPolicyResponse> GetKeyPolicyAsync(string keyId, string policyName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<GetKeyRotationStatusResponse> GetKeyRotationStatusAsync(GetKeyRotationStatusRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<GetKeyRotationStatusResponse> GetKeyRotationStatusAsync(string keyId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<GetParametersForImportResponse> GetParametersForImportAsync(GetParametersForImportRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<GetPublicKeyResponse> GetPublicKeyAsync(GetPublicKeyRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ImportKeyMaterialResponse> ImportKeyMaterialAsync(ImportKeyMaterialRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ListAliasesResponse> ListAliasesAsync(ListAliasesRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ListGrantsResponse> ListGrantsAsync(ListGrantsRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ListKeyPoliciesResponse> ListKeyPoliciesAsync(ListKeyPoliciesRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ListKeyRotationsResponse> ListKeyRotationsAsync(ListKeyRotationsRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ListKeysResponse> ListKeysAsync(ListKeysRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ListResourceTagsResponse> ListResourceTagsAsync(ListResourceTagsRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ListRetirableGrantsResponse> ListRetirableGrantsAsync(ListRetirableGrantsRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ListRetirableGrantsResponse> ListRetirableGrantsAsync(string retiringPrincipal, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ListRetirableGrantsResponse> ListRetirableGrantsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<PutKeyPolicyResponse> PutKeyPolicyAsync(PutKeyPolicyRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<PutKeyPolicyResponse> PutKeyPolicyAsync(string keyId, string policy, string policyName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ReEncryptResponse> ReEncryptAsync(ReEncryptRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ReplicateKeyResponse> ReplicateKeyAsync(ReplicateKeyRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<RetireGrantResponse> RetireGrantAsync(RetireGrantRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<RetireGrantResponse> RetireGrantAsync(string grantToken, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<RevokeGrantResponse> RevokeGrantAsync(RevokeGrantRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<RevokeGrantResponse> RevokeGrantAsync(string grantId, string keyId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<RotateKeyOnDemandResponse> RotateKeyOnDemandAsync(RotateKeyOnDemandRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ScheduleKeyDeletionResponse> ScheduleKeyDeletionAsync(ScheduleKeyDeletionRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ScheduleKeyDeletionResponse> ScheduleKeyDeletionAsync(string keyId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ScheduleKeyDeletionResponse> ScheduleKeyDeletionAsync(string keyId, int? pendingWindowInDays, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<SignResponse> SignAsync(SignRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<TagResourceResponse> TagResourceAsync(TagResourceRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<UntagResourceResponse> UntagResourceAsync(UntagResourceRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<UpdateAliasResponse> UpdateAliasAsync(UpdateAliasRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<UpdateAliasResponse> UpdateAliasAsync(string aliasName, string targetKeyId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<UpdateCustomKeyStoreResponse> UpdateCustomKeyStoreAsync(UpdateCustomKeyStoreRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<UpdateKeyDescriptionResponse> UpdateKeyDescriptionAsync(UpdateKeyDescriptionRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<UpdateKeyDescriptionResponse> UpdateKeyDescriptionAsync(string keyId, string description, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<UpdatePrimaryRegionResponse> UpdatePrimaryRegionAsync(UpdatePrimaryRegionRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<VerifyResponse> VerifyAsync(VerifyRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<VerifyMacResponse> VerifyMacAsync(VerifyMacRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Amazon.Runtime.Endpoints.Endpoint DetermineServiceOperationEndpoint(AmazonWebServiceRequest request)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
