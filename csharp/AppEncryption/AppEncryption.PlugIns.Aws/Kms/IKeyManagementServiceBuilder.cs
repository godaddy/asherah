using Amazon.Runtime;
using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Kms
{
    /// <summary>
    /// Builder for KeyManagementServiceOptions.
    /// </summary>
    public interface IKeyManagementServiceBuilder
    {
        /// <summary>
        /// Adds a logger factory to the builder. Required for logging within the KeyManagementService.
        /// </summary>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/></param>
        /// <returns><see cref="IKeyManagementServiceBuilder"/></returns>
        IKeyManagementServiceBuilder WithLoggerFactory(ILoggerFactory loggerFactory);

        /// <summary>
        /// Credentials are needed to create the AWS KMS clients for each region.
        /// This is not required if providing your own <see cref="IKeyManagementClientFactory"/>
        /// which can handle credentials itself.
        /// </summary>
        /// <param name="credentials"><see cref="AWSCredentials"/></param>
        /// <returns><see cref="IKeyManagementServiceBuilder"/></returns>
        IKeyManagementServiceBuilder WithCredentials(AWSCredentials credentials);

        /// <summary>
        /// Use to provied your own implementation of <see cref="IKeyManagementClientFactory"/>.
        /// This allows your application to customize the creation of the AWS KMS clients for each region requested.
        /// </summary>
        /// <param name="kmsClientFactory"><see cref="IKeyManagementClientFactory"/></param>
        /// <returns><see cref="IKeyManagementServiceBuilder"/></returns>
        IKeyManagementServiceBuilder WithKmsClientFactory(IKeyManagementClientFactory kmsClientFactory);

        /// <summary>
        /// Adds a region and key ARN pair to the builder. Note that this can be called multiple times to add multiple regions.
        /// The order of the regions should be from most preferred to least preferred.
        /// </summary>
        /// <param name="region">A valid AWS region</param>
        /// <param name="keyArn">The KMS key Arn from that region to use as your master key</param>
        /// <returns><see cref="IKeyManagementServiceBuilder"/></returns>
        IKeyManagementServiceBuilder WithRegionKeyArn(string region, string keyArn);

        /// <summary>
        /// Used to provide all the regions and key Anrs at once using a strongly typed options object.
        /// This can easily be deserialized from a configuration file and used instead of calling WithRegionKeyArn multiple times.
        /// </summary>
        /// <param name="options"><see cref="KeyManagementServiceOptions"/></param>
        /// <returns><see cref="IKeyManagementServiceBuilder"/></returns>
        IKeyManagementServiceBuilder WithOptions(KeyManagementServiceOptions options);

        /// <summary>
        /// Returns a new instance of <see cref="KeyManagementService"/> with the configured options.
        /// </summary>
        /// <returns></returns>
        KeyManagementService Build();
    }
}
