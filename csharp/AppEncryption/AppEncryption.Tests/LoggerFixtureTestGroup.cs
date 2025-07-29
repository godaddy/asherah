using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests
{
    [CollectionDefinition("Logger Fixture collection")]
    public class LoggerFixtureTestGroup : ICollectionFixture<LoggerFixture>
    {
        // Intentionally blank. Just sets up the Attribute
    }
}
