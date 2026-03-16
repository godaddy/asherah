using System;

namespace GoDaddy.Asherah.AppEncryption.Metastore
{
    /// <summary>
    /// Represents a readonly interface for metadata for a parent key.
    /// </summary>
    public interface IKeyMeta
    {
        /// <summary>
        /// Gets the key identifier.
        /// </summary>
        string KeyId { get; }

        /// <summary>
        /// Gets the creation time of the key.
        /// </summary>
        DateTimeOffset Created { get; }
    }
}
