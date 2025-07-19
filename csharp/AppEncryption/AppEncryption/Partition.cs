using System;

namespace GoDaddy.Asherah.AppEncryption
{
    /// <summary>
    /// An additional layer of abstraction which generates the system key and intermediate key ids.
    /// It uses a <see cref="PartitionId"/> to uniquely identify a <see cref="Session{TP,TD}"/>, i.e. every partition id
    /// should have its own session.
    /// A payload encrypted using some partition id, cannot be decrypted using a different one.
    /// </summary>
    public abstract class Partition
    {
        protected Partition(string partitionId, string serviceId, string productId)
        {
            PartitionId = partitionId;
            ServiceId = serviceId;
            ProductId = productId;
        }

        /// <summary>
        /// Gets get the system key id.
        /// </summary>
        public virtual string SystemKeyId => "_SK_" + ServiceId + "_" + ProductId;

        /// <summary>
        /// Gets get the intermediate key id.
        /// </summary>
        public virtual string IntermediateKeyId => "_IK_" + PartitionId + "_" + ServiceId + "_" + ProductId;

        internal string PartitionId { get; }

        internal string ServiceId { get; }

        internal string ProductId { get; }

        public virtual bool IsValidIntermediateKeyId(string keyId)
        {
            return keyId.Equals(IntermediateKeyId, StringComparison.Ordinal);
        }
    }
}
