using System;

namespace GoDaddy.Asherah.SecureMemory
{
    public abstract class Secret : IDisposable
    {
        public abstract TResult WithSecretBytes<TResult>(Func<byte[], TResult> funcWithSecret);

        public abstract TResult WithSecretUtf8Chars<TResult>(Func<char[], TResult> funcWithSecret);

        public abstract TResult WithSecretIntPtr<TResult>(Func<IntPtr, ulong, TResult> funcWithSecret);

        public void WithSecretBytes(Action<byte[]> actionWithSecret)
        {
            WithSecretBytes(bytes =>
            {
                actionWithSecret(bytes);
                return true;
            });
        }

        public void WithSecretUtf8Chars(Action<char[]> actionWithSecret)
        {
            WithSecretUtf8Chars(chars =>
            {
                actionWithSecret(chars);
                return true;
            });
        }

        public void WithSecretIntPtr(Action<IntPtr, ulong> actionWithSecret)
        {
            WithSecretIntPtr((ptr, len) =>
            {
                actionWithSecret(ptr, len);
                return true;
            });
        }

        public abstract Secret CopySecret();

        public abstract void Close();

        public abstract void Dispose();
    }
}
