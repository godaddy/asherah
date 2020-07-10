using System;
using GoDaddy.Asherah.AppEncryption.Util;
using Newtonsoft.Json.Linq;

namespace GoDaddy.Asherah.AppEncryption.Envelope
{
    /// <summary>
    /// The <see cref="KeyMeta"/> format is defined as:
    /// <code>
    /// {
    ///   KeyId: "some_key_id",
    ///   Created: 1534553054
    /// }
    /// </code>
    /// </summary>
    public class KeyMeta
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KeyMeta"/> class using the provided parameters. Key meta is the
        /// metadata in <see cref="EnvelopeKeyRecord"/> that references a parent key in the key hierarchy.
        /// Note that for system keys, this content may be embedded within the encrypted key content, depending on the
        /// KMS being used.
        /// </summary>
        ///
        /// <param name="keyId">The key id.</param>
        /// <param name="created">The creation time of the key.</param>
        public KeyMeta(string keyId, DateTimeOffset created)
        {
            KeyId = keyId;
            Created = created;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyMeta"/> class from the source json.
        /// </summary>
        ///
        /// <param name="sourceJson">Source json to create the key meta.</param>
        public KeyMeta(Json sourceJson)
        {
            KeyId = sourceJson.GetString("KeyId");
            Created = sourceJson.GetDateTimeOffset("Created");
        }

        internal string KeyId { get; }

        internal virtual DateTimeOffset Created { get; }

        /// <summary>
        /// Converts the <see cref="KeyMeta"/> to a <see cref="JObject"/> with below format:
        /// <code>
        /// {
        ///   "KeyId": "some_key_id",
        ///   "Created": 1534553054
        /// }
        /// </code>
        /// </summary>
        ///
        /// <returns>The <see cref="KeyMeta"/> converted to a <see cref="JObject"/> object.</returns>
        public JObject ToJson()
        {
            Json json = new Json();
            json.Put("KeyId", KeyId);
            json.Put("Created", Created);
            return json.ToJObject();
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }

            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            KeyMeta other = (KeyMeta)obj;
            return Equals(KeyId, other.KeyId)
                   && Equals(Created, other.Created);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return (KeyId, Created).GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "KeyMeta [KeyId=" + KeyId + ", Created=" + Created + "]";
        }
    }
}
