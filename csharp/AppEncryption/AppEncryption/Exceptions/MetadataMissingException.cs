using GoDaddy.Asherah.Crypto.Exceptions;

namespace GoDaddy.Asherah.AppEncryption.Exceptions
{
    /// <inheritdoc />
    public class MetadataMissingException : AppEncryptionException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataMissingException"/> class. This signals that a
        /// <see cref="GoDaddy.Asherah.AppEncryption.Persistence.IMetastore{T}"/> exception has occured.
        /// </summary>
        ///
        /// <param name="message">The detailed exception message.</param>
        public MetadataMissingException(string message)
            : base(message)
        {
        }
    }
}
