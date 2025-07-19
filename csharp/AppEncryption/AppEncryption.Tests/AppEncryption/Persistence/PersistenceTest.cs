using System;
using GoDaddy.Asherah.AppEncryption.Persistence;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    [Collection("Logger Fixture collection")]
    public class PersistenceTest
    {
        private readonly Mock<Persistence<string>> persistenceMock;

        public PersistenceTest()
        {
            persistenceMock = new Mock<Persistence<string>>();
        }

        [Fact]
        private void TestStore()
        {
            const string expectedPersistenceKey = "some_key";
            persistenceMock.Setup(x => x.GenerateKey(It.IsAny<string>())).Returns(expectedPersistenceKey);
            persistenceMock.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<string>())).Verifiable();
            persistenceMock.Setup(x => x.Store(It.IsAny<string>())).CallBase();

            const string value = "some_value";
            string actualPersistenceKey = persistenceMock.Object.Store(value);
            Assert.Equal(expectedPersistenceKey, actualPersistenceKey);
            persistenceMock.Verify(x => x.GenerateKey(value));
            persistenceMock.Verify(x => x.Store(expectedPersistenceKey, value));
        }

        [Fact]
        private void TestGenerateKey()
        {
            const string value = "unused_value";
            persistenceMock.Setup(x => x.GenerateKey(It.IsAny<string>())).CallBase();

            string returnedValue = persistenceMock.Object.GenerateKey(value);

            // Just verify it's a valid UUID
            Assert.True(Guid.TryParse(returnedValue, out Guid guidOutput));
        }
    }
}
