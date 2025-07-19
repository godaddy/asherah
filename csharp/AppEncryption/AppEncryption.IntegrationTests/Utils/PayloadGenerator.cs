using System;
using System.Text;
using Newtonsoft.Json.Linq;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils
{
    public static class PayloadGenerator
    {
        private const int DefaultByteSize = 20;
        private const string PermittedCharacters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        private static readonly Random Random = new Random();

        public static byte[] CreateDefaultRandomBytePayload()
        {
            return CreateRandomBytePayload(DefaultByteSize);
        }

        public static byte[] CreateRandomBytePayload(int size)
        {
            return Encoding.UTF8.GetBytes(RandomStringGenerator(size, Random));
        }

        public static JObject CreateDefaultRandomJsonPayload()
        {
            return CreateRandomJsonPayload(DefaultByteSize);
        }

        public static JObject CreateRandomJsonPayload(int size)
        {
            // This will end up having an extra 10 bytes from json overhead + key, meh
            JObject json = new JObject();
            json.Add("key", RandomStringGenerator(size, Random));
            return json;
        }

        private static string RandomStringGenerator(int length, Random random)
        {
            StringBuilder result = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                result.Append(PermittedCharacters[random.Next(PermittedCharacters.Length)]);
            }

            return result.ToString();
        }
    }
}
