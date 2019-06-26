using System;
using GoDaddy.Asherah.AppEncryption.Persistence;
using LanguageExt;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    [Collection("Logger Fixture collection")]
    public class AdhocPersistenceTest
    {
        private readonly Mock<Func<string, Option<string>>> persistenceLoadMock;
        private readonly Mock<Action<string, string>> persistenceStoreMock;

        private readonly Persistence<string> adhocPersistence;

        public AdhocPersistenceTest()
        {
            persistenceLoadMock = new Mock<Func<string, Option<string>>>();
            persistenceStoreMock = new Mock<Action<string, string>>();
            adhocPersistence = new AdhocPersistence<string>(persistenceLoadMock.Object, persistenceStoreMock.Object);
        }

        [Fact]
        private void TestConstructor()
        {
            Persistence<string> adhocPersistence = new AdhocPersistence<string>(persistenceLoadMock.Object, persistenceStoreMock.Object);
            Assert.NotNull(adhocPersistence);
        }

        [Fact]
        private void TestLoad()
        {
            const string expectedValue = "some_value";
            const string someKey = "some_key";
            persistenceLoadMock.Setup(x => x(It.IsAny<string>())).Returns(expectedValue);
            Option<string> actualValue = adhocPersistence.Load(someKey);

            Assert.Equal(expectedValue, actualValue);
            persistenceLoadMock.Verify(x => x(someKey));
        }

        [Fact]
        private void TestStore()
        {
            const string key = "some_key";
            const string value = "some_value";

            adhocPersistence.Store(key, value);

            persistenceStoreMock.Verify(x => x(key, value));
        }
    }
}
