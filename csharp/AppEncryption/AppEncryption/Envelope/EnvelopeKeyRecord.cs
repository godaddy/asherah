using System;
using System.Runtime.CompilerServices;
using GoDaddy.Asherah.AppEncryption.Util;
using LanguageExt;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("AppEncryption.IntegrationTests")]

namespace GoDaddy.Asherah.AppEncryption.Envelope
{
    /// <summary>
    /// The <see cref="EnvelopeKeyRecord"/> format is:
    /// <code>
    /// {
    ///   Created: UTC epoch in seconds of when the key was created Identifier data of parent key (which encrypts this
    ///            key),
    ///   ParentKeyMeta: {
    ///     KeyId: KeyId of the parent key,
    ///     Created: Created timestamp of parent key
    ///   },
    ///   Key: Base64 converted value of "Key encrypted with the parent key",
    ///   Revoked: The revocation status of the key (True/False)
    /// }
    /// </code>
    /// NOTE: For system key, the parent <see cref="KeyMeta"/> (in this case the master key identifier) may instead be a
    /// part of the Key content, depending on the master key type.
    /// </summary>
    public class EnvelopeKeyRecord
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EnvelopeKeyRecord"/> class using the provided parameters. An
        /// envelope key record is an internal data structure used to represent a system key, intermediate key or a data
        /// row key. It consists of an encrypted key and metadata referencing the parent key in the key hierarchy used
        /// to encrypt it (i.e. its Key Encryption Key).
        /// </summary>
        ///
        /// <param name="created">Creation time of the <paramref name="encryptedKey"/>.</param>.
        /// <param name="parentKeyMeta">The <see cref="KeyMeta"/> for encryption keys.</param>
        /// <param name="encryptedKey">The encrypted key (a system key, intermediate key or a data row key.</param>
        public EnvelopeKeyRecord(DateTimeOffset created,  KeyMeta parentKeyMeta,  byte[] encryptedKey)
            : this(created, parentKeyMeta, encryptedKey, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvelopeKeyRecord"/> class using the provided parameters. An
        /// envelope key record is an internal data structure used to represent a system key, intermediate key or a data
        /// row key. It consists of an encrypted key and metadata referencing the parent key in the key hierarchy used
        /// to encrypt it (i.e. its Key Encryption Key).
        /// </summary>
        ///
        /// <param name="created">Creation time of the <paramref name="encryptedKey"/>.</param>.
        /// <param name="parentKeyMeta">The <see cref="KeyMeta"/> for encryption keys.</param>
        /// <param name="encryptedKey">The encrypted key (a system key, intermediate key or a data row key.</param>
        /// <param name="revoked">The revocation status of the encrypted key.</param>
        public EnvelopeKeyRecord(DateTimeOffset created,  KeyMeta parentKeyMeta,  byte[] encryptedKey,  bool? revoked)
        {
            Created = created;
            ParentKeyMeta = parentKeyMeta;
            EncryptedKey = encryptedKey;
            Revoked = revoked ?? Option<bool>.None;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvelopeKeyRecord"/> class.
        /// </summary>
        /// <param name="sourceJson">The source json file to use.</param>
        public EnvelopeKeyRecord(Json sourceJson)
        {
            Created = sourceJson.GetDateTimeOffset("Created");
            ParentKeyMeta = sourceJson.GetOptionalJson("ParentKeyMeta").Map(x => new KeyMeta(x));
            EncryptedKey = sourceJson.GetBytes("Key");
            Revoked = sourceJson.GetOptionalBoolean("Revoked");
        }

        internal DateTimeOffset Created { get; }

        internal Option<KeyMeta> ParentKeyMeta { get; }

        internal Option<bool> Revoked { get; }

        internal byte[] EncryptedKey { get; }

        /// <summary>
        /// Converts the <see cref="EnvelopeKeyRecord"/> to a <see cref="JObject"/> format:
        /// <code>
        /// {
        ///   "Created": Creation time of the encrypted key,
        ///   "ParentKeyMeta": Parent key meta of the encrypted key(if present),
        ///   "Key": Encrypted Key,
        ///   "Revoked": True/False
        /// }
        /// </code>
        /// </summary>
        ///
        /// <returns>The <see cref="EnvelopeKeyRecord"/> converted to a <see cref="JObject"/>.</returns>
        public JObject ToJson()
        {
            Json json = new Json();
            json.Put("Created", Created);
            json.Put("Key", EncryptedKey);
            ParentKeyMeta.IfSome(keyMeta => json.Put("ParentKeyMeta", keyMeta.ToJson()));
            Revoked.IfSome(revoked => json.Put("Revoked", revoked));
            return json.ToJObject();
        }
    }
}
