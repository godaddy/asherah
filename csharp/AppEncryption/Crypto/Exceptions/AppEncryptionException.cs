using System;

namespace GoDaddy.Asherah.Crypto.Exceptions
{
    /// <inheritdoc />
    public class AppEncryptionException : SystemException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AppEncryptionException"/> class. This signals that a
        /// <see cref="GoDaddy.Asherah.Crypto.Envelope.AeadEnvelopeCrypto"/> exception has occured.
        /// </summary>
        ///
        /// <param name="message">The detailed exception message.</param>
        public AppEncryptionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AppEncryptionException"/> class. This signals that a
        /// <see cref="GoDaddy.Asherah.Crypto.Envelope.AeadEnvelopeCrypto"/> exception has occured.
        /// </summary>
        ///
        /// <param name="message">The detailed exception message.</param>
        /// <param name="cause">The actual <see cref="Exception"/> raised.</param>
        public AppEncryptionException(string message, Exception cause)
            : base(message, cause)
        {
        }
    }
}
