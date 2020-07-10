using GoDaddy.Asherah.Crypto.Exceptions;

namespace GoDaddy.Asherah.AppEncryption.Exceptions
{
    public class KmsException : AppEncryptionException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KmsException"/> class. This signals that a
        /// <see cref="GoDaddy.Asherah.AppEncryption.Kms.KeyManagementService"/> exception has occured.
        /// </summary>
        ///
        /// <param name="message">The detailed exception message.</param>
        public KmsException(string message)
            : base(message)
        {
        }
    }
}
