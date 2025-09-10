namespace GoDaddy.Asherah.Crypto.Envelope
{
    public class EnvelopeEncryptResult<T> where T : class
    {
        public byte[] CipherText { get; set; }

        public byte[] EncryptedKey { get; set; }

        public T UserState { get; set; }
    }

    // For backwards compatibility
    public class EnvelopeEncryptResult : EnvelopeEncryptResult<object>
    {
    }
}
