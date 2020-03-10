using System.Runtime.CompilerServices;
using Org.BouncyCastle.Utilities;

namespace GoDaddy.Asherah.Crypto.BufferUtils
{
    public static class ManagedBufferUtils
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void WipeByteArray(byte[] sensitiveData)
        {
            // NoOptimize to prevent the optimizer from deciding this call is unnecessary
            // NoInlining to prevent the inliner from forgetting that the method was no-optimize
            Arrays.Fill(sensitiveData, 0);
        }
    }
}
