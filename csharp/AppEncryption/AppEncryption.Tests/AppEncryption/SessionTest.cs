using GoDaddy.Asherah.AppEncryption.Persistence;
using LanguageExt;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption
{
    [Collection("Logger Fixture collection")]
    public class SessionTest
    {
        private readonly Mock<Persistence<string>> persistenceMock;

        private readonly Mock<Session<string, string>> sessionMock;

        public SessionTest()
        {
            persistenceMock = new Mock<Persistence<string>>();
            sessionMock = new Mock<Session<string, string>>();
        }

        [Fact]
        private void TestLoadWithNonEmptyValue()
        {
            string dataRowRecord = "some data row record";
            persistenceMock.Setup(x => x.Load(It.IsAny<string>())).Returns(Option<string>.Some(dataRowRecord));
            string expectedPayload = "some_payload";
            sessionMock.Setup(x => x.Decrypt(It.IsAny<string>())).Returns(expectedPayload);
            sessionMock.Setup(x => x.Load(It.IsAny<string>(), It.IsAny<Persistence<string>>())).CallBase();

            string persistenceKey = "some_key";
            Option<string> actualPayload = sessionMock.Object.Load(persistenceKey, persistenceMock.Object);
            Assert.True(actualPayload.IsSome);
            Assert.Equal(expectedPayload, actualPayload);
            persistenceMock.Verify(x => x.Load(persistenceKey));
            sessionMock.Verify(x => x.Decrypt(dataRowRecord));
        }

        [Fact]
        private void TestLoadWithEmptyValue()
        {
            persistenceMock.Setup(x => x.Load(It.IsAny<string>())).Returns(Option<string>.None);
            sessionMock.Setup(x => x.Load(It.IsAny<string>(), It.IsAny<Persistence<string>>())).CallBase();

            string persistenceKey = "key_with_no_value";
            Option<string> actualPayload = sessionMock.Object.Load(persistenceKey, persistenceMock.Object);
            Assert.False(actualPayload.IsSome);
            persistenceMock.Verify(x => x.Load(persistenceKey));
            sessionMock.Verify(x => x.Decrypt(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        private void TestStore()
        {
            string dataRowRecord = "some data row record";
            sessionMock.Setup(x => x.Encrypt(It.IsAny<string>())).Returns(dataRowRecord);
            string expectedPersistenceKey = "some_key";
            persistenceMock.Setup(x => x.Store(It.IsAny<string>())).Returns(expectedPersistenceKey);
            sessionMock.Setup(x => x.Store(It.IsAny<string>(), It.IsAny<Persistence<string>>())).CallBase();

            string payload = "some_payload";
            string actualPersistenceKey = sessionMock.Object.Store(payload, persistenceMock.Object);
            Assert.Equal(expectedPersistenceKey, actualPersistenceKey);
            sessionMock.Verify(x => x.Encrypt(payload));
            persistenceMock.Verify(x => x.Store(dataRowRecord));
        }

        [Fact]
        private void TestStoreWithPersistenceKey()
        {
            string dataRowRecord = "some data row record";
            string persistenceKey = "some_key";
            string payload = "some_payload";
            sessionMock.Setup(x => x.Encrypt(payload)).Returns(dataRowRecord);
            sessionMock.Setup(x => x.Store(persistenceKey, payload, persistenceMock.Object)).CallBase();

            sessionMock.Object.Store(persistenceKey, payload, persistenceMock.Object);
            sessionMock.Verify(x => x.Encrypt(payload), Times.Once);
            persistenceMock.Verify(x => x.Store(persistenceKey, dataRowRecord), Times.Once);
        }
    }
}
