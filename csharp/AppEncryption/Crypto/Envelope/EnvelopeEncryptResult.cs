namespace GoDaddy.Asherah.Crypto.Envelope
{
    public class EnvelopeEncryptResult
    {
        public byte[] CipherText { get; set; }

        public byte[] EncryptedKey { get; set; }

        // TODO Consider refactoring this somehow. Ends up always being KeyMeta
        public object UserState { get; set; }
    }
}
