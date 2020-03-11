using System;
using GoDaddy.Asherah.Crypto.Keys;

namespace GoDaddy.Asherah.AppEncryption.Kms
{
    public abstract class KeyManagementService
    {
        public abstract byte[] EncryptKey(CryptoKey key);

        public abstract CryptoKey DecryptKey(byte[] keyCipherText, DateTimeOffset keyCreated, bool revoked);

        public virtual TResult WithDecryptedKey<TResult>(
            byte[] keyCipherText,
            DateTimeOffset keyCreated,
            bool revoked,
            Func<CryptoKey, DateTimeOffset, TResult> actionWithDecryptedKey)
        {
            using (CryptoKey key = DecryptKey(keyCipherText, keyCreated, revoked))
            {
                return actionWithDecryptedKey.Invoke(key, keyCreated);
            }
        }
    }
}
