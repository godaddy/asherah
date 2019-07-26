using Xunit;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests
{
    [CollectionDefinition("Configuration collection")]
    public class ConfigCollection : ICollectionFixture<ConfigFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}