namespace GoDaddy.Asherah.AppEncryption.Core
{
    /// <summary>
    /// Represents a partition for encryption operations.
    /// </summary>
    public interface ISessionPartition
    {
        /// <summary>
        /// Gets the partition id.
        /// </summary>
        string PartitionId { get; }

        /// <summary>
        /// Gets the service id.
        /// </summary>
        string ServiceId { get; }

        /// <summary>
        /// Gets the product id.
        /// </summary>
        string ProductId { get; }

        /// <summary>
        /// Gets the optional suffix appended to key ids.
        /// </summary>
        string Suffix { get; }

        /// <summary>
        /// Gets the system key id.
        /// </summary>
        string SystemKeyId { get; }

        /// <summary>
        /// Gets the intermediate key id.
        /// </summary>
        string IntermediateKeyId { get; }

        /// <summary>
        /// Validates whether the given key id matches this partition's intermediate key id.
        /// </summary>
        /// <param name="keyId">The key id to validate.</param>
        /// <returns>True if the key id matches this partition's intermediate key id.</returns>
        bool IsValidIntermediateKeyId(string keyId);
    }
}
