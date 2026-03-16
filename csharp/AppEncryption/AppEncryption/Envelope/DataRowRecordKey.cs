using System;
using GoDaddy.Asherah.AppEncryption.Metastore;

namespace GoDaddy.Asherah.AppEncryption.Envelope
{
    /// <summary>
    /// Internal model representing the key portion of a DataRowRecord.
    /// This is specifically for data row keys and does not include revocation status.
    /// </summary>
    internal class DataRowRecordKey : IKeyRecord
    {
        /// <summary>
        /// Gets or sets the creation timestamp of the encrypted key.
        /// </summary>
        public DateTimeOffset Created { get; set; }

        /// <summary>
        /// Gets or sets the base64-encoded encrypted data row key byte array.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the revocation status of the encrypted key.
        /// Data row keys are never revoked, so this always returns null.
        /// </summary>
        public bool? Revoked => null;

        /// <summary>
        /// Gets or sets the metadata for the parent key, if any.
        /// </summary>
        public IKeyMeta ParentKeyMeta { get; set; }
    }
}
