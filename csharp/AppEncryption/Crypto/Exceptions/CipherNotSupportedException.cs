using System;

namespace GoDaddy.Asherah.Crypto.Exceptions
{
    public class CipherNotSupportedException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CipherNotSupportedException"/> class. This signals that the
        /// cipher being used is not supported by the library.
        /// </summary>
        ///
        /// <param name="message">The detailed exception message.</param>
        public CipherNotSupportedException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CipherNotSupportedException"/> class. This signals that the
        /// cipher being used is not supported by the library.
        /// </summary>
        ///
        /// <param name="message">The detailed exception message.</param>
        /// <param name="inner">The actual <see cref="Exception"/> raised.</param>
        public CipherNotSupportedException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
