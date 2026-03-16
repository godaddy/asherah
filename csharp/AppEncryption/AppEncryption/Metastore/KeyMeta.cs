using System;

namespace GoDaddy.Asherah.AppEncryption.Metastore
{
    /// <summary>
    /// Represents metadata for a parent key.
    /// </summary>
    public class KeyMeta : IKeyMeta
    {
        /// <summary>
        /// Gets or sets the key identifier.
        /// </summary>
        public string KeyId { get; set; }

        /// <summary>
        /// Gets or sets the creation time of the key.
        /// </summary>
        public DateTimeOffset Created { get; set; }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }

            if (obj == null)
            {
                return false;
            }

            var other = obj as KeyMeta;
            if (other == null)
            {
                return false;
            }

            return KeyId.Equals(other.KeyId, StringComparison.Ordinal) && Created.Equals(other.Created);
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
