using System;

namespace GoDaddy.Asherah.AppEncryption.Core
{
    /// <summary>
    /// Represents a partition for encryption operations, generating system key and intermediate key ids.
    /// </summary>
    public class SessionPartition : ISessionPartition
    {
        private const string SystemKeyPrefix = "_SK_";
        private const string IntermediateKeyPrefix = "_IK_";
        private const string Separator = "_";

        private readonly string _baseIntermediateKeyId;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionPartition"/> class.
        /// </summary>
        /// <param name="partitionId">The unique identifier for this partition.</param>
        /// <param name="serviceId">The unique identifier for the service.</param>
        /// <param name="productId">The unique identifier for the product.</param>
        /// <param name="suffix">Optional suffix appended to key ids (e.g., region identifier for DynamoDB Global Tables).</param>
        public SessionPartition(string partitionId, string serviceId, string productId, string suffix = null)
        {
            PartitionId = partitionId;
            ServiceId = serviceId;
            ProductId = productId;
            Suffix = suffix;

            var baseKeyPart = serviceId + Separator + productId;
            var baseSystemKeyId = SystemKeyPrefix + baseKeyPart;
            _baseIntermediateKeyId = IntermediateKeyPrefix + partitionId + Separator + baseKeyPart;

            if (!string.IsNullOrEmpty(suffix))
            {
                SystemKeyId = baseSystemKeyId + Separator + suffix;
                IntermediateKeyId = _baseIntermediateKeyId + Separator + suffix;
            }
            else
            {
                SystemKeyId = baseSystemKeyId;
                IntermediateKeyId = _baseIntermediateKeyId;
            }
        }

        /// <summary>
        /// Gets the partition id.
        /// </summary>
        public string PartitionId { get; }

        /// <summary>
        /// Gets the service id.
        /// </summary>
        public string ServiceId { get; }

        /// <summary>
        /// Gets the product id.
        /// </summary>
        public string ProductId { get; }

        /// <summary>
        /// Gets the optional suffix appended to key ids.
        /// </summary>
        public string Suffix { get; }

        /// <summary>
        /// Gets the system key id.
        /// </summary>
        public string SystemKeyId { get; }

        /// <summary>
        /// Gets the intermediate key id.
        /// </summary>
        public string IntermediateKeyId { get; }

        /// <inheritdoc/>
        public bool IsValidIntermediateKeyId(string keyId)
        {
            // Exact match with full key id (including suffix if present)
            if (string.Equals(keyId, IntermediateKeyId, StringComparison.Ordinal))
            {
                return true;
            }

            // If we have a suffix, also allow matching the base key id (for backwards compatibility)
            if (!string.IsNullOrEmpty(Suffix))
            {
                return keyId.StartsWith(_baseIntermediateKeyId, StringComparison.Ordinal);
            }

            return false;
        }
    }
}
