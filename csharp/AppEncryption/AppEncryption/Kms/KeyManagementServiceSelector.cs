using System;
using System.Collections.Generic;
using System.Linq;
using GoDaddy.Asherah.Crypto;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.AppEncryption.Kms
{
    public class KeyManagementServiceSelector
    {
        public const string KmsRegionToArn = "kmsAwsRegionArnTuples";
        public const string KmsStaticKey = "kmsStaticKey";
        public const string KmsPreferredRegion = "kmsAwsPreferredRegion";
        public const string KmsAws = "aws";
        public const string KmsStatic = "static";
        public const string KmsType = "kmsType";

        public static KeyManagementService SelectKmsWithConfiguration(CryptoPolicy cryptoPolicy, IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var keyManagementServiceType = configuration[KmsType];
            if (string.IsNullOrWhiteSpace(keyManagementServiceType))
            {
                throw new Exception($"Missing {KmsType} in configuration");
            }

            if (keyManagementServiceType.Equals(KmsAws, StringComparison.InvariantCultureIgnoreCase))
            {
                var kmsClientFactory = new AwsKmsClientFactory();
                var preferredRegion = configuration[KmsPreferredRegion];
                if (string.IsNullOrWhiteSpace(preferredRegion))
                {
                    throw new Exception($"Missing {KmsPreferredRegion} in configuration");
                }

                var regionToArnTuples = configuration[KmsRegionToArn];

                Dictionary<string, string> regionToArnDictionary =
                    regionToArnTuples.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(part => part.Split('='))
                        .ToDictionary(split => split[0], split => split[1]);

                return new AwsKeyManagementServiceImpl(regionToArnDictionary, preferredRegion, cryptoPolicy.GetCrypto(), kmsClientFactory);
            }

            if (keyManagementServiceType.Equals(KmsStatic, StringComparison.InvariantCultureIgnoreCase))
            {
                var kmsStaticKey = configuration[KmsStaticKey];
                if (string.IsNullOrWhiteSpace(kmsStaticKey))
                {
                    throw new Exception($"No {KmsStaticKey} specified!");
                }

                return new StaticKeyManagementServiceImpl(kmsStaticKey, cryptoPolicy, configuration);
            }

            throw new Exception($"Unknown {KmsType}: " + keyManagementServiceType);
        }
    }
}
