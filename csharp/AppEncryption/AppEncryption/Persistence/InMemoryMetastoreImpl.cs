using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using LanguageExt;

namespace GoDaddy.Asherah.AppEncryption.Persistence
{
    public class InMemoryMetastoreImpl<T> : IMetastore<T>
    {
        private readonly DataTable dataTable;

        public InMemoryMetastoreImpl()
        {
            dataTable = new DataTable();
            dataTable.Columns.Add("keyId", typeof(string));
            dataTable.Columns.Add("created", typeof(DateTimeOffset));
            dataTable.Columns.Add("value", typeof(T));
            dataTable.PrimaryKey = new[] { dataTable.Columns["keyId"], dataTable.Columns["created"] };
        }

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
