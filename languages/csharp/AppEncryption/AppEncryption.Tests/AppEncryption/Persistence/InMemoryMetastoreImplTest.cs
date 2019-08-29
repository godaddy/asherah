using System;
using GoDaddy.Asherah.AppEncryption.Persistence;
using LanguageExt;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    [Collection("Logger Fixture collection")]
    public class InMemoryMetastoreImplTest
    {
        private readonly InMemoryMetastoreImpl<string> inMemoryMetastoreImpl;

        public InMemoryMetastoreImplTest()
        {
            inMemoryMetastoreImpl = new InMemoryMetastoreImpl<string>();
        }

        [Fact]
        private void TestLoadAndStoreWithValidKey()
        {
            const string keyId = "ThisIsMyKey";
            DateTimeOffset created = DateTimeOffset.UtcNow;
            const string value = "This is my value";

            inMemoryMetastoreImpl.Store(keyId, created, value);

            Option<string> actualValue = inMemoryMetastoreImpl.Load(keyId, created);

            Assert.Equal(value, actualValue);
        }

        [Fact]
        private void TestLoadAndStoreWithInvalidKey()
        {
            const string keyId = "ThisIsMyKey";
            DateTimeOffset created = DateTimeOffset.UtcNow;
            const string value = "This is my value";

            inMemoryMetastoreImpl.Store(keyId, created, value);

            Option<string> actualValue = inMemoryMetastoreImpl.Load("some non-existent key", created);

            Assert.True(actualValue.IsNone);
        }

        [Fact]
        private void TestLoadLatestMultipleCreatedAndValuesForKeyIdShouldReturnLatest()
        {
            const string keyId = "ThisIsMyKey";
            DateTimeOffset created = DateTimeOffset.UtcNow;
            const string value = "This is my value";

            inMemoryMetastoreImpl.Store(keyId, created, value);

            DateTimeOffset createdOneHourLater = created.AddHours(1);
            string valueCreatedOneHourLater = value + createdOneHourLater;
            inMemoryMetastoreImpl.Store(keyId, createdOneHourLater, valueCreatedOneHourLater);

            DateTimeOffset createdOneDayLater = created.AddDays(1);
            string valueCreatedOneDayLater = value + createdOneDayLater;
            inMemoryMetastoreImpl.Store(keyId, createdOneDayLater, valueCreatedOneDayLater);

            DateTimeOffset createdOneWeekEarlier = created.AddDays(-7);
            string valueCreatedOneWeekEarlier = value + createdOneWeekEarlier;
            inMemoryMetastoreImpl.Store(keyId, createdOneWeekEarlier, valueCreatedOneWeekEarlier);

            Option<string> loadLatest = inMemoryMetastoreImpl.LoadLatest(keyId);

            Assert.Equal(valueCreatedOneDayLater, loadLatest);
        }

        [Fact]
        private void TestLoadLatestNonExistentKeyIdShouldReturnNull()
        {
            const string keyId = "ThisIsMyKey";
            DateTimeOffset created = DateTimeOffset.UtcNow;
            const string value = "This is my value";

            inMemoryMetastoreImpl.Store(keyId, created, value);

            Assert.True(inMemoryMetastoreImpl.LoadLatest("some non-existent key").IsNone);
        }

        [Fact]
        private void TestStoreWithDuplicateKeyShouldReturnFalse()
        {
            const string keyId = "ThisIsMyKey";
            DateTimeOffset created = DateTimeOffset.UtcNow;
            const string value = "This is my value";

            Assert.True(inMemoryMetastoreImpl.Store(keyId, created, value));
            Assert.False(inMemoryMetastoreImpl.Store(keyId, created, value));
        }
    }
}
