using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using GoDaddy.Asherah.AppEncryption.Metastore;

namespace GoDaddy.Asherah.AppEncryption.PlugIns.Testing.Metastore
{
    /// <summary>
    /// Provides a volatile implementation of <see cref="IKeyMetastore"/> for key records using a
    /// <see cref="System.Data.DataTable"/>. NOTE: This should NEVER be used in a production environment.
    /// </summary>
    public class InMemoryKeyMetastore : IKeyMetastore, IDisposable
    {
        private readonly DataTable _dataTable;
        private int _failNextStoreCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryKeyMetastore"/> class, with 3 columns.
        /// <code>
        /// keyId | created | keyRecord
        /// ----- | ------- | ---------
        ///       |         |
        ///       |         |
        /// </code>
        /// Uses 'keyId' and 'created' as the primary key.
        /// </summary>
        public InMemoryKeyMetastore()
        {
            _dataTable = new DataTable();
            _dataTable.Columns.Add("keyId", typeof(string));
            _dataTable.Columns.Add("created", typeof(DateTimeOffset));
            _dataTable.Columns.Add("keyRecord", typeof(KeyRecord));
            _dataTable.PrimaryKey = new[] { _dataTable.Columns["keyId"], _dataTable.Columns["created"] };
        }

        /// <inheritdoc />
        public Task<(bool found, IKeyRecord keyRecord)> TryLoadAsync(string keyId, DateTimeOffset created)
        {
            lock (_dataTable)
            {
                var dataRows = _dataTable.Rows.Cast<DataRow>()
                    .Where(row => row["keyId"].Equals(keyId)
                                  && row["created"].Equals(created))
                    .ToList();
                if (dataRows.Count == 0)
                {
                    return Task.FromResult((false, (IKeyRecord)null));
                }

                var keyRecord = (IKeyRecord)dataRows.Single()["keyRecord"];
                return Task.FromResult((true, keyRecord));
            }
        }

        /// <inheritdoc />
        public Task<(bool found, IKeyRecord keyRecord)> TryLoadLatestAsync(string keyId)
        {
            lock (_dataTable)
            {
                var dataRows = _dataTable.Rows.Cast<DataRow>()
                    .Where(row => row["keyId"].Equals(keyId))
                    .OrderBy(row => row["created"])
                    .ToList();

                // Need to check if empty as Last will throw an exception instead of returning null
                if (dataRows.Count == 0)
                {
                    return Task.FromResult((false, (IKeyRecord)null));
                }

                var keyRecord = (IKeyRecord)dataRows.Last()["keyRecord"];
                return Task.FromResult((true, keyRecord));
            }
        }

        /// <inheritdoc />
        public Task<bool> StoreAsync(string keyId, DateTimeOffset created, IKeyRecord keyRecord)
        {
            lock (_dataTable)
            {
                // Check if we should simulate a duplicate/failure
                if (_failNextStoreCount > 0)
                {
                    _failNextStoreCount--;
                    // Still store the record (simulating another process stored it first)
                    // but return false to indicate duplicate
                    var existingRows = _dataTable.Rows.Cast<DataRow>()
                        .Where(row => row["keyId"].Equals(keyId)
                                      && row["created"].Equals(created))
                        .ToList();
                    if (existingRows.Count == 0)
                    {
                        _dataTable.Rows.Add(keyId, created, keyRecord);
                    }

                    return Task.FromResult(false);
                }

                var dataRows = _dataTable.Rows.Cast<DataRow>()
                    .Where(row => row["keyId"].Equals(keyId)
                                  && row["created"].Equals(created))
                    .ToList();
                if (dataRows.Count > 0)
                {
                    return Task.FromResult(false);
                }

                _dataTable.Rows.Add(keyId, created, keyRecord);
                return Task.FromResult(true);
            }
        }

        /// <inheritdoc />
        public string GetKeySuffix()
        {
            return string.Empty;
        }

        #region Test Helper Methods

        /// <summary>
        /// Sets the number of subsequent StoreAsync calls that should simulate a duplicate detection.
        /// The store will still save the record but return false, simulating another process storing first.
        /// This is useful for testing duplicate key handling and retry logic.
        /// </summary>
        /// <param name="count">The number of store calls to fail.</param>
        public void FailNextStores(int count = 1)
        {
            lock (_dataTable)
            {
                _failNextStoreCount = count;
            }
        }

        /// <summary>
        /// Deletes a key record from the metastore. This is a test helper method not part of <see cref="IKeyMetastore"/>.
        /// </summary>
        /// <param name="keyId">The key ID to delete.</param>
        /// <param name="created">The created timestamp of the key to delete.</param>
        /// <returns>True if the key was found and deleted, false otherwise.</returns>
        public bool DeleteKey(string keyId, DateTimeOffset created)
        {
            lock (_dataTable)
            {
                var dataRow = _dataTable.Rows.Cast<DataRow>()
                    .SingleOrDefault(row => row["keyId"].Equals(keyId)
                                            && row["created"].Equals(created));

                if (dataRow == null)
                {
                    return false;
                }

                _dataTable.Rows.Remove(dataRow);
                return true;
            }
        }

        /// <summary>
        /// Marks a key record as revoked. This is a test helper method not part of <see cref="IKeyMetastore"/>.
        /// </summary>
        /// <param name="keyId">The key ID to mark as revoked.</param>
        /// <param name="created">The created timestamp of the key to mark as revoked.</param>
        /// <returns>True if the key was found and updated, false otherwise.</returns>
        public bool MarkKeyAsRevoked(string keyId, DateTimeOffset created)
        {
            lock (_dataTable)
            {
                var dataRow = _dataTable.Rows.Cast<DataRow>()
                    .SingleOrDefault(row => row["keyId"].Equals(keyId)
                                            && row["created"].Equals(created));

                if (dataRow == null)
                {
                    return false;
                }

                var existingRecord = (KeyRecord)dataRow["keyRecord"];
                var revokedRecord = new KeyRecord(
                    existingRecord.Created,
                    existingRecord.Key,
                    true, // Mark as revoked
                    existingRecord.ParentKeyMeta);

                dataRow["keyRecord"] = revokedRecord;
                return true;
            }
        }

        /// <summary>
        /// Clears the ParentKeyMeta of a key record, making it appear as if the key has no parent.
        /// This is useful for testing error handling when ParentKeyMeta is null.
        /// </summary>
        /// <param name="keyId">The key ID to modify.</param>
        /// <param name="created">The created timestamp of the key to modify.</param>
        /// <returns>True if the key was found and modified, false otherwise.</returns>
        public bool ClearParentKeyMeta(string keyId, DateTimeOffset created)
        {
            lock (_dataTable)
            {
                var dataRow = _dataTable.Rows.Cast<DataRow>()
                    .SingleOrDefault(row => row["keyId"].Equals(keyId)
                                            && row["created"].Equals(created));

                if (dataRow == null)
                {
                    return false;
                }

                var existingRecord = (KeyRecord)dataRow["keyRecord"];
                var modifiedRecord = new KeyRecord(
                    existingRecord.Created,
                    existingRecord.Key,
                    existingRecord.Revoked,
                    null); // Clear ParentKeyMeta

                dataRow["keyRecord"] = modifiedRecord;
                return true;
            }
        }

        /// <summary>
        /// Gets the system key ID for a partition by loading the latest IK and returning its ParentKeyMeta.
        /// This is a test helper method for setting up test scenarios.
        /// </summary>
        /// <param name="intermediateKeyId">The intermediate key ID to look up.</param>
        /// <returns>The system key metadata (KeyId and Created) if found, null otherwise.</returns>
        public (string keyId, DateTimeOffset created)? GetSystemKeyMetaForIntermediateKey(string intermediateKeyId)
        {
            lock (_dataTable)
            {
                var dataRow = _dataTable.Rows.Cast<DataRow>()
                    .Where(row => row["keyId"].Equals(intermediateKeyId))
                    .OrderBy(row => row["created"])
                    .LastOrDefault();

                if (dataRow == null)
                {
                    return null;
                }

                var ikRecord = (KeyRecord)dataRow["keyRecord"];
                if (ikRecord.ParentKeyMeta == null)
                {
                    return null;
                }

                return (ikRecord.ParentKeyMeta.KeyId, ikRecord.ParentKeyMeta.Created);
            }
        }

        #endregion

        /// <summary>
        /// Disposes of the managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of the managed resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose, false if called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            lock (_dataTable)
            {
                _dataTable?.Dispose();
            }
        }
    }
}
