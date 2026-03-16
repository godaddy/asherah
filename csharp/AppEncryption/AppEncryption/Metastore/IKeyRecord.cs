using System;

namespace GoDaddy.Asherah.AppEncryption.Metastore
{
    /// <summary>
    /// Represents a readonly interface for a key record with basic properties for encrypted keys.
    /// System KeyRecords will not have a ParentKeyMeta, while Intermediate KeyRecords will have a ParentKeyMeta.
    /// </summary>
    public interface IKeyRecord
    {
        /// <summary>
        /// Gets the creation time of the encrypted key.
        /// </summary>
        DateTimeOffset Created { get; }

        /// <summary>
        /// Gets the encoded/encrypted key data as a string.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the revocation status of the encrypted key.
        /// </summary>
        bool? Revoked { get; }

        /// <summary>
        /// Gets the metadata for the parent key, if any.
        /// </summary>
        IKeyMeta ParentKeyMeta { get; }
    }
}
