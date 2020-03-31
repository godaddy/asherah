using System;
using System.Runtime.CompilerServices;
using GoDaddy.Asherah.AppEncryption.Persistence;
using LanguageExt;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.AppEncryption
{
    /// <summary>
    /// Primary interface for using the app encryption library.
    /// </summary>
    /// <typeparam name="TP">The payload type of the data being encrypted (e.g. JSON, String, etc.)</typeparam>
    /// <typeparam name="TD">The Data Row Record type being used to store it and any supporting metadata.</typeparam>
    public abstract class Session<TP, TD> : IDisposable
    {
        public abstract void Dispose();

        /// <summary>
        /// Decrypts a Data Row Record based on an implementation-specific encryption algorithm and returns the actual
        /// payload.
        /// </summary>
        /// <param name="dataRowRecord"> The DRR to be decrypted</param>
        /// <returns>The decrypted payload</returns>
        public abstract TP Decrypt(TD dataRowRecord);

        /// <summary>
        /// Encrypts a payload using an implementation-specific encryption algorithm and returns the Data Row Record
        /// that contains it.
        /// </summary>
        /// <param name="payLoad">The payload to be encrypted</param>
        /// <returns>The Data Row Record that contains the now-encrypted payload</returns>
        public abstract TD Encrypt(TP payLoad);

        /// <summary>
        /// Uses a persistence key to load a Data Row Record from the provided data persistence store, if any,
        /// and returns the decrypted payload.
        /// </summary>
        /// <param name="persistenceKey">Key used to retrieve the Data Row Record</param>
        /// <param name="dataPersistence">The persistence store from which to retrieve the DRR</param>
        /// <returns>The decrypted payload, if found in persistence</returns>
        public virtual Option<TP> Load(string persistenceKey, Persistence<TD> dataPersistence)
        {
            return dataPersistence.Load(persistenceKey).Map(Decrypt);
        }

        /// <summary>
        /// Encrypts a payload, stores the resulting Data Row Record into the provided data persistence store, and
        /// returns its associated persistence key for future lookups.
        /// </summary>
        /// <param name="payload">Payload to be encrypted</param>
        /// <param name="dataPersistence">The persistence store where the encrypted DRR should be stored</param>
        /// <returns>The persistence key associated with the stored Data Row Record</returns>
        public virtual string Store(TP payload, Persistence<TD> dataPersistence)
        {
            TD dataRowRecord = Encrypt(payload);
            return dataPersistence.Store(dataRowRecord);
        }

        /// <summary>
        /// Encrypts a payload, stores the resulting Data Row Record into the provided data persistence store with
        /// given key
        /// </summary>
        /// <param name="key">Key against which the encrypted DRR will be saved</param>
        /// <param name="payload">Payload to be encrypted</param>
        /// <param name="dataPersistence">The persistence store where the encrypted DRR should be stored</param>
        public virtual void Store(string key, TP payload, Persistence<TD> dataPersistence)
        {
            TD dataRowRecord = Encrypt(payload);
            dataPersistence.Store(key, dataRowRecord);
        }
    }
}
