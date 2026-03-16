using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using GoDaddy.Asherah.AppEncryption.Metastore;
using GoDaddy.Asherah.AppEncryption.PlugIns.Testing.Metastore;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Metastore
{
    [ExcludeFromCodeCoverage]
    public class InMemoryKeyMetastoreTest : IDisposable
    {
        private readonly InMemoryKeyMetastore _inMemoryKeyMetastore = new();

        [Fact]
        private async Task TestTryLoadAndStoreWithValidKey()
        {
            const string keyId = "ThisIsMyKey";
            var created = DateTimeOffset.UtcNow;
            var keyRecord = new KeyRecord(created, "test-key-data", false);

            await _inMemoryKeyMetastore.StoreAsync(keyId, created, keyRecord);

            var (success, actualKeyRecord) = await _inMemoryKeyMetastore.TryLoadAsync(keyId, created);

            Assert.True(success);
            Assert.Equal(keyRecord, actualKeyRecord);
        }

        [Fact]
        private async Task TestTryLoadAndStoreWithInvalidKey()
        {
            const string keyId = "ThisIsMyKey";
            var created = DateTimeOffset.UtcNow;
            var keyRecord = new KeyRecord(created, "test-key-data", false);

            await _inMemoryKeyMetastore.StoreAsync(keyId, created, keyRecord);

            var (success, actualKeyRecord) = await _inMemoryKeyMetastore.TryLoadAsync("some non-existent key", created);

            Assert.False(success);
            Assert.Null(actualKeyRecord);
        }

        [Fact]
        private async Task TestTryLoadLatestMultipleCreatedAndValuesForKeyIdShouldReturnLatest()
        {
            const string keyId = "ThisIsMyKey";
            var created = DateTimeOffset.UtcNow;
            var keyRecord = new KeyRecord(created, "test-key-data", false);

            await _inMemoryKeyMetastore.StoreAsync(keyId, created, keyRecord);

            var createdOneHourLater = created.AddHours(1);
            var keyRecordOneHourLater = new KeyRecord(createdOneHourLater, "test-key-data-hour", false);
            await _inMemoryKeyMetastore.StoreAsync(keyId, createdOneHourLater, keyRecordOneHourLater);

            var createdOneDayLater = created.AddDays(1);
            var keyRecordOneDayLater = new KeyRecord(createdOneDayLater, "test-key-data-day", false);
            await _inMemoryKeyMetastore.StoreAsync(keyId, createdOneDayLater, keyRecordOneDayLater);

            var createdOneWeekEarlier = created.AddDays(-7);
            var keyRecordOneWeekEarlier = new KeyRecord(createdOneWeekEarlier, "test-key-data-week", false);
            await _inMemoryKeyMetastore.StoreAsync(keyId, createdOneWeekEarlier, keyRecordOneWeekEarlier);

            var (success, actualKeyRecord) = await _inMemoryKeyMetastore.TryLoadLatestAsync(keyId);

            Assert.True(success);
            Assert.Equal(keyRecordOneDayLater, actualKeyRecord);
        }

        [Fact]
        private async Task TestTryLoadLatestNonExistentKeyIdShouldReturnFalse()
        {
            const string keyId = "ThisIsMyKey";
            var created = DateTimeOffset.UtcNow;
            var keyRecord = new KeyRecord(created, "test-key-data", false);

            await _inMemoryKeyMetastore.StoreAsync(keyId, created, keyRecord);

            var (success, actualKeyRecord) = await _inMemoryKeyMetastore.TryLoadLatestAsync("some non-existent key");

            Assert.False(success);
            Assert.Null(actualKeyRecord);
        }

        [Fact]
        private async Task TestStoreWithDuplicateKeyShouldReturnFalse()
        {
            const string keyId = "ThisIsMyKey";
            var created = DateTimeOffset.UtcNow;
            var keyRecord = new KeyRecord(created, "test-key-data", false);

            Assert.True(await _inMemoryKeyMetastore.StoreAsync(keyId, created, keyRecord));
            Assert.False(await _inMemoryKeyMetastore.StoreAsync(keyId, created, keyRecord));
        }

        [Fact]
        private async Task TestStoreWithIntermediateKeyRecord()
        {
            const string keyId = "ThisIsMyKey";
            var created = DateTimeOffset.UtcNow;
            var parentKeyMeta = new KeyMeta { KeyId = "parentKey", Created = created.AddDays(-1) };
            var keyRecord = new KeyRecord(created, "test-key-data-parent", false, parentKeyMeta);

            var success = await _inMemoryKeyMetastore.StoreAsync(keyId, created, keyRecord);

            Assert.True(success);

            var (loadSuccess, actualKeyRecord) = await _inMemoryKeyMetastore.TryLoadAsync(keyId, created);
            Assert.True(loadSuccess);
            Assert.Equal(keyRecord, actualKeyRecord);
        }

        [Fact]
        private void TestGetKeySuffixReturnsEmptyString()
        {
            var keySuffix = _inMemoryKeyMetastore.GetKeySuffix();
            Assert.Equal(string.Empty, keySuffix);
        }

        /// <summary>
        /// Disposes of the managed resources.
        /// </summary>
        public void Dispose()
        {
            _inMemoryKeyMetastore?.Dispose();
        }
    }
}
