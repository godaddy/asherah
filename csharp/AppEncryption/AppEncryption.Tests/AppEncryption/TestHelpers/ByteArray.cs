using System.Linq;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.TestHelpers
{
    public static class ByteArray
    {
        public static bool IsAllZeros(byte[] input)
        {
            return input.All(b => b == 0);
        }
    }
}
