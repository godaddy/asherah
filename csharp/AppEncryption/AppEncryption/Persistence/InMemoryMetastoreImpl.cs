using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using LanguageExt;

namespace GoDaddy.Asherah.AppEncryption.Persistence
{
    /// <summary>
    /// Provides a volatile implementation of <see cref="IMetastore{T}"/> for values using a
    /// <see cref="System.Data.DataTable"/>. NOTE: This should NEVER be used in a production environment.
    /// </summary>
    ///
    /// <typeparam name="T">The type of value to store and retrieve.</typeparam>
    public class InMemoryMetastoreImpl<T> : IMetastore<T>
    {
        private readonly DataTable dataTable;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryMetastoreImpl{T}"/> class, with 3 columns.
        /// <code>
        /// keyId | created | value
        /// ----- | ------- | ------
        ///       |         |
        ///       |         |
        /// </code>
        /// Uses 'keyId' and 'created' as the primary key.
        /// </summary>
        public InMemoryMetastoreImpl()
        {
            dataTable = new DataTable();
            dataTable.Columns.Add("keyId", typeof(string));
            dataTable.Columns.Add("created", typeof(DateTimeOffset));
            dataTable.Columns.Add("value", typeof(T));
            dataTable.PrimaryKey = new[] { dataTable.Columns["keyId"], dataTable.Columns["created"] };
        }

        /// <inheritdoc />
        public virtual Option<T> Load(string keyId, DateTimeOffset created)
        {
            lock (dataTable)
            {
                List<DataRow> dataRows = dataTable.Rows.Cast<DataRow>()
                    .Where(row => row["keyId"].Equals(keyId)
                                  && row["created"].Equals(created))
                    .ToList();
                if (!dataRows.Any())
                {
                    return Option<T>.None;
                }

                return (T)dataRows.Single()["value"];
            }
        }

        /// <summary>
        /// Obtain the latest value associated with the keyId by ordering the datatable in ascending order.
        /// </summary>
        ///
        /// <param name="keyId">The keyId to lookup.</param>
        /// <returns>The latest <see cref="T"/> value associated with the keyId, if any.</returns>
        public virtual Option<T> LoadLatest(string keyId)
        {
            lock (dataTable)
            {
                List<DataRow> dataRows = dataTable.Rows.Cast<DataRow>()
                    .Where(row => row["keyId"].Equals(keyId))
                    .OrderBy(row => row["created"])
                    .ToList();

                // Need to check if empty as Last will throw an exception instead of returning null
                if (!dataRows.Any())
                {
                    return Option<T>.None;
                }

                return (T)dataRows.Last()["value"];
            }
        }

        /// <inheritdoc />
        public virtual bool Store(string keyId, DateTimeOffset created, T value)
        {
            lock (dataTable)
            {
                List<DataRow> dataRows = dataTable.Rows.Cast<DataRow>()
                    .Where(row => row["keyId"].Equals(keyId)
                                  && row["created"].Equals(created))
                    .ToList();
                if (dataRows.Any())
                {
                    return false;
                }

                dataTable.Rows.Add(keyId, created, value);
                return true;
            }
        }
    }
}
