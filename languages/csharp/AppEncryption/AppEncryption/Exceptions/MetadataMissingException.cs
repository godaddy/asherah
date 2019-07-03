using GoDaddy.Asherah.Crypto.Exceptions;

namespace GoDaddy.Asherah.AppEncryption.Exceptions
{
    public class MetadataMissingException : AppEncryptionException
    {
        public MetadataMissingException(string message)
            : base(message)
        {
        }
    }
}
