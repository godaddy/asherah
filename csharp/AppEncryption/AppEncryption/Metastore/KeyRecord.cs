using System;

namespace GoDaddy.Asherah.AppEncryption.Metastore
{
    /// <summary>
    /// Represents a key record with basic properties for encrypted keys.
    /// System KeyRecords will not have a ParentKeyMeta, while Intermediate KeyRecords will have a ParentKeyMeta.
    /// </summary>
    public class KeyRecord : IKeyRecord
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KeyRecord"/> class.
        /// System KeyRecords will not have a ParentKeyMeta, while Intermediate KeyRecords will have a ParentKeyMeta.
        /// </summary>
        ///
        /// <param name="created">Creation time of the encrypted key.</param>
        /// <param name="key">The encoded/encrypted key data as a string.</param>
        /// <param name="revoked">The revocation status of the encrypted key.</param>
        /// <param name="parentKeyMeta">The metadata for the parent key, if any. Defaults to null for system keys.</param>
        public KeyRecord(DateTimeOffset created, string key, bool? revoked, IKeyMeta parentKeyMeta = null)
        {
            Created = created;
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Revoked = revoked;
            ParentKeyMeta = parentKeyMeta;
        }

        /// <summary>
        /// Gets the creation time of the encrypted key.
        /// </summary>
        public DateTimeOffset Created { get; }

        /// <summary>
        /// Gets the encoded/encrypted key data as a string.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the revocation status of the encrypted key.
        /// </summary>
        public bool? Revoked { get; }

        /// <summary>
        /// Gets the metadata for the parent key, if any.
        /// </summary>
        public IKeyMeta ParentKeyMeta { get; }


    }
}
