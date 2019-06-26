using System;
using System.Threading;
using GoDaddy.Asherah.Crypto.Keys;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.Crypto.Keys
{
    [Collection("Logger Fixture collection")]
    public class SecureCryptoKeyDictionaryTest : IDisposable
    {
        private const long RevokeCheckPeriodMillis = 1000;
        private readonly Mock<SharedCryptoKey> sharedCryptoKeyMock;
        private readonly Mock<CryptoKey> cryptoKeyMock;
        private readonly SecureCryptoKeyDictionary<string> secureCryptoKeyDictionary;

        public SecureCryptoKeyDictionaryTest()
        {
            cryptoKeyMock = new Mock<CryptoKey>();
            sharedCryptoKeyMock = new Mock<SharedCryptoKey>(cryptoKeyMock.Object);
            secureCryptoKeyDictionary = new SecureCryptoKeyDictionary<string>(RevokeCheckPeriodMillis);
        }

        public void Dispose()
        {
            secureCryptoKeyDictionary.Dispose();
        }

        [Fact]
        private void TestGetWithNullSecret()
        {
            string key = "null_key";
            Assert.Null(secureCryptoKeyDictionary.Get(key));
        }

        [Fact]
        private void TestGetWithRevokedKeyShouldReturnKey()
        {
            string key = "some_key";
            sharedCryptoKeyMock.Setup(x => x.IsRevoked()).Returns(true);
            secureCryptoKeyDictionary.PutAndGetUsable(key, sharedCryptoKeyMock.Object);

            CryptoKey actualCryptoKey = secureCryptoKeyDictionary.Get(key);

            Assert.IsType<SharedCryptoKey>(actualCryptoKey);
            SharedCryptoKey sharedCrypto = (SharedCryptoKey)actualCryptoKey;
            Assert.Equal(sharedCryptoKeyMock.Object, sharedCrypto.SharedKey);
            Assert.True(actualCryptoKey.IsRevoked());
        }

        [Fact]
        private void TestGetWithRevokeCheckExpiredShouldReturnNull()
        {
            using (SecureCryptoKeyDictionary<string> secureCryptoKeyDictionary = new SecureCryptoKeyDictionary<string>(1))
            {
                string key = "some_key";
                secureCryptoKeyDictionary.PutAndGetUsable(key, sharedCryptoKeyMock.Object);

                // sleep to trigger period check flow
                Thread.Sleep(3);

                CryptoKey actualCryptoKey = secureCryptoKeyDictionary.Get(key);
                Assert.Null(actualCryptoKey);
            }
        }

        [Fact]
        private void TestGetLastWithEmptyMapShouldReturnNull()
        {
            Assert.Null(secureCryptoKeyDictionary.GetLast());
        }

        [Fact]
        private void TestGetLastWithRevokedKeyShouldReturnKey()
        {
            string key = "some_key";
            sharedCryptoKeyMock.Setup(x => x.IsRevoked()).Returns(true);
            secureCryptoKeyDictionary.PutAndGetUsable(key, sharedCryptoKeyMock.Object);

            CryptoKey actualCryptoKey = secureCryptoKeyDictionary.GetLast();
            Assert.IsType<SharedCryptoKey>(actualCryptoKey);
            SharedCryptoKey sharedCrypto = (SharedCryptoKey)actualCryptoKey;
            Assert.Equal(sharedCryptoKeyMock.Object, sharedCrypto.SharedKey);
            Assert.True(actualCryptoKey.IsRevoked());
        }

        [Fact]
        private void TestGetLastWithRevokeCheckExpiredShouldReturnNull()
        {
            using (SecureCryptoKeyDictionary<string> secureCryptoKeyDictionary = new SecureCryptoKeyDictionary<string>(1))
            {
                string key = "some_key";
                secureCryptoKeyDictionary.PutAndGetUsable(key, sharedCryptoKeyMock.Object);

                // sleep to trigger period check flow
                Thread.Sleep(3);

                CryptoKey actualCryptoKey = secureCryptoKeyDictionary.GetLast();
                Assert.Null(actualCryptoKey);
            }
        }

        [Fact]
        private void TestSimplePutAndGet()
        {
            string key = "some_key";
            secureCryptoKeyDictionary.PutAndGetUsable(key, sharedCryptoKeyMock.Object);
            CryptoKey actualCryptoKey = secureCryptoKeyDictionary.Get(key);
            Assert.NotNull(actualCryptoKey);
            Assert.IsType<SharedCryptoKey>(actualCryptoKey);
        }

        [Fact]
        private void TestPutMultipleAndGetLast()
        {
            Mock<CryptoKey> cryptoKey1 = new Mock<CryptoKey>();
            Mock<CryptoKey> cryptoKey2 = new Mock<CryptoKey>();
            Mock<CryptoKey> cryptoKey3 = new Mock<CryptoKey>();
            Mock<CryptoKey> cryptoKey4 = new Mock<CryptoKey>();
            Mock<CryptoKey> cryptoKey5 = new Mock<CryptoKey>();
            secureCryptoKeyDictionary.PutAndGetUsable("klhjasdffghs", cryptoKey1.Object);
            secureCryptoKeyDictionary.PutAndGetUsable("zzzzzzzz", cryptoKey2.Object); // should always be last since sorted
            secureCryptoKeyDictionary.PutAndGetUsable("ghtew", cryptoKey3.Object);
            secureCryptoKeyDictionary.PutAndGetUsable("asdfasdfasdf", cryptoKey4.Object);
            secureCryptoKeyDictionary.PutAndGetUsable("aaaaaaaa", cryptoKey5.Object);
            CryptoKey lastCryptoKey = secureCryptoKeyDictionary.GetLast();
            Assert.IsType<SharedCryptoKey>(lastCryptoKey);
            SharedCryptoKey sharedCrypto = (SharedCryptoKey)lastCryptoKey;
            Assert.Equal(cryptoKey2.Object, sharedCrypto.SharedKey);
        }

        [Fact]
        private void TestDuplicatePutAndGetUsable()
        {
            CryptoKey returnValue = secureCryptoKeyDictionary.PutAndGetUsable("test", cryptoKeyMock.Object);
            Assert.NotEqual(cryptoKeyMock.Object, returnValue);
            Assert.IsType<SharedCryptoKey>(returnValue);
            CryptoKey returnValueTwo = secureCryptoKeyDictionary.PutAndGetUsable("test", cryptoKeyMock.Object);
            Assert.Equal(cryptoKeyMock.Object, returnValueTwo);
        }

        [Fact]
        private void TestPutAndGetUsableWithNotRevokedShouldUpdateReturnNullAfterCheckPeriodAndNotNullAfterPutRefreshes()
        {
            // Give it enough time to account for timing differences
            using (SecureCryptoKeyDictionary<string> secureCryptoKeyDictionary = new SecureCryptoKeyDictionary<string>(50))
            {
                secureCryptoKeyDictionary.PutAndGetUsable("test", cryptoKeyMock.Object);
                CryptoKey getResult = secureCryptoKeyDictionary.Get("test");
                Assert.NotNull(getResult);

                // Sleep for enough time to get null result
                Thread.Sleep(100);
                getResult = secureCryptoKeyDictionary.Get("test");
                Assert.Null(getResult);

                // Put back in to refresh cached time so we can get non-null result again
                secureCryptoKeyDictionary.PutAndGetUsable("test", cryptoKeyMock.Object);
                getResult = secureCryptoKeyDictionary.Get("test");
                Assert.NotNull(getResult);
            }
        }

        [Fact]
        private void TestPutAndGetUsableWithUpdateRevokedShouldMarkRevokedAndReturnNotNull()
        {
            // Give it enough time to account for timing differences
            using (SecureCryptoKeyDictionary<string> secureCryptoKeyDictionary = new SecureCryptoKeyDictionary<string>(50))
            {
                secureCryptoKeyDictionary.PutAndGetUsable("test", cryptoKeyMock.Object);
                CryptoKey getResult = secureCryptoKeyDictionary.Get("test");
                Assert.NotNull(getResult);

                // Sleep for enough time to get null result
                Thread.Sleep(100);
                getResult = secureCryptoKeyDictionary.Get("test");
                Assert.Null(getResult);

                // Put back in as revoked so we can get non-null result again
                cryptoKeyMock.Setup(x => x.IsRevoked()).Returns(true);
                secureCryptoKeyDictionary.PutAndGetUsable("test", cryptoKeyMock.Object);
                getResult = secureCryptoKeyDictionary.Get("test");
                Assert.NotNull(getResult);

                // Sleep for enough time to get null result and verify we still get non-null
                Thread.Sleep(30);
                getResult = secureCryptoKeyDictionary.Get("test");
                Assert.NotNull(getResult);
            }
        }

        [Fact]
        private void TestKeyCloseIsCalledOnKeyDictionaryClose()
        {
            secureCryptoKeyDictionary.PutAndGetUsable("some_key", sharedCryptoKeyMock.Object);
            secureCryptoKeyDictionary.Dispose();
            sharedCryptoKeyMock.Verify(x => x.Dispose());
        }

        [Fact]
        private void TestKeyCloseIsCalledOnKeyDictionaryCloseAndSecondCloseIsNoop()
        {
            secureCryptoKeyDictionary.PutAndGetUsable("some_key", sharedCryptoKeyMock.Object);
            secureCryptoKeyDictionary.Dispose();
            secureCryptoKeyDictionary.Dispose();
            sharedCryptoKeyMock.Verify(x => x.Dispose(), Times.Once);
        }

        [Fact]
        private void TestKeyDictionaryThrowsExceptionOnGetAfterClose()
        {
            string key = "some_key";
            secureCryptoKeyDictionary.PutAndGetUsable(key, sharedCryptoKeyMock.Object);
            secureCryptoKeyDictionary.Dispose();
            Assert.Throws<InvalidOperationException>(() => secureCryptoKeyDictionary.Get(key));
        }

        [Fact]
        private void TestKeyDictionaryThrowsExceptionOnGetLastAfterClose()
        {
            secureCryptoKeyDictionary.Dispose();
            Assert.Throws<InvalidOperationException>(() => secureCryptoKeyDictionary.GetLast());
        }

        [Fact]
        private void TestKeyDictionaryThrowsExceptionOnPutAfterClose()
        {
            string key = "some_key";
            secureCryptoKeyDictionary.PutAndGetUsable(key, sharedCryptoKeyMock.Object);
            secureCryptoKeyDictionary.Dispose();
            Assert.Throws<InvalidOperationException>(() =>
                secureCryptoKeyDictionary.PutAndGetUsable(key, sharedCryptoKeyMock.Object));
        }
    }
}
