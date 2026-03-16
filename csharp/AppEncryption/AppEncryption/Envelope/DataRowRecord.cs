using GoDaddy.Asherah.AppEncryption.Metastore;

namespace GoDaddy.Asherah.AppEncryption.Envelope
{
    /// <summary>
    /// Internal model representing a DataRowRecord with strongly-typed structure.
    /// This replaces the generic byte[] approach with a concrete model that matches the JSON structure.
    /// </summary>
    internal class DataRowRecord
    {
        /// <summary>
        /// Gets or sets the key portion containing the encrypted data row key and metadata.
        /// </summary>
        public IKeyRecord Key { get; set; }

        /// <summary>
        /// Gets or sets the base64-encoded encrypted data byte array.
        /// </summary>
        public string Data { get; set; }
    }
}
