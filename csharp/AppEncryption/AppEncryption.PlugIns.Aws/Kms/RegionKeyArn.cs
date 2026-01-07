using System.Text.Json.Serialization;

namespace GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Kms
{
    /// <summary>
    /// Represents a region and its corresponding KMS key ARN.
    /// </summary>
    public class RegionKeyArn
    {
        /// <summary>
        /// Gets or sets the AWS region name.
        /// </summary>
        [JsonPropertyName("region")]
        public string Region { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the KMS key ARN for the region.
        /// </summary>
        [JsonPropertyName("keyArn")]
        public string KeyArn { get; set; } = string.Empty;
    }
}
