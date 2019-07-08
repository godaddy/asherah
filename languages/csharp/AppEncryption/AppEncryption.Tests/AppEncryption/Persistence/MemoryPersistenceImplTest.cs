using System;
using GoDaddy.Asherah.AppEncryption.Persistence;
using LanguageExt;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    [Collection("Logger Fixture collection")]
    public class MemoryPersistenceImplTest
    {
        private readonly MemoryPersistenceImpl<string> memoryPersistenceImpl;

        public MemoryPersistenceImplTest()
        {
            memoryPersistenceImpl = new MemoryPersistenceImpl<string>();
        }

        [Fact]
        private void TestLoadAndStoreWithValidKey()
        {
            const string keyId = "ThisIsMyKey";
            DateTimeOffset created = DateTimeOffset.UtcNow;
            const string value = "This is my value";

            memoryPersistenceImpl.Store(keyId, created, value);

            Option<string> actualValue = memoryPersistenceImpl.Load(keyId, created);

            Assert.Equal(value, actualValue);
        }

        [Fact]
        private void TestLoadAndStoreWithInvalidKey()
        {
            const string keyId = "ThisIsMyKey";
            DateTimeOffset created = DateTimeOffset.UtcNow;
            const string value = "This is my value";

            memoryPersistenceImpl.Store(keyId, created, value);

            Option<string> actualValue = memoryPersistenceImpl.Load("some non-existent key", created);

            Assert.True(actualValue.IsNone);
        }

        [Fact]
        private void TestLoadLatestValueMultipleCreatedAndValuesForKeyIdShouldReturnLatest()
        {
            const string keyId = "ThisIsMyKey";
            DateTimeOffset created = DateTimeOffset.UtcNow;
            const string value = "This is my value";

            memoryPersistenceImpl.Store(keyId, created, value);

            DateTimeOffset createdOneHourLater = created.AddHours(1);
            string valueCreatedOneHourLater = value + createdOneHourLater;
            memoryPersistenceImpl.Store(keyId, createdOneHourLater, valueCreatedOneHourLater);

            DateTimeOffset createdOneDayLater = created.AddDays(1);
            string valueCreatedOneDayLater = value + createdOneDayLater;
            memoryPersistenceImpl.Store(keyId, createdOneDayLater, valueCreatedOneDayLater);

            DateTimeOffset createdOneWeekEarlier = created.AddDays(-7);
            string valueCreatedOneWeekEarlier = value + createdOneWeekEarlier;
            memoryPersistenceImpl.Store(keyId, createdOneWeekEarlier, valueCreatedOneWeekEarlier);

            Option<string> loadLatestValue = memoryPersistenceImpl.LoadLatestValue(keyId);

            Assert.Equal(valueCreatedOneDayLater, loadLatestValue);
        }

        [Fact]
        private void TestLoadLatestValueNonExistentKeyIdShouldReturnNull()
        {
            const string keyId = "ThisIsMyKey";
            DateTimeOffset created = DateTimeOffset.UtcNow;
            const string value = "This is my value";

            memoryPersistenceImpl.Store(keyId, created, value);

            Assert.True(memoryPersistenceImpl.LoadLatestValue("some non-existent key").IsNone);
        }

        [Fact]
        private void TestStoreWithDuplicateKeyShouldReturnFalse()
        {
            const string keyId = "ThisIsMyKey";
            DateTimeOffset created = DateTimeOffset.UtcNow;
            const string value = "This is my value";

            Assert.True(memoryPersistenceImpl.Store(keyId, created, value));
            Assert.False(memoryPersistenceImpl.Store(keyId, created, value));
        }
    }
}
