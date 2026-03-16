using System.Threading.Tasks;

namespace GoDaddy.Asherah.AppEncryption.Core
{
    /// <summary>
    /// A wrapper around <see cref="EncryptionSession"/> that prevents consumers from
    /// disposing the underlying session. Used when session caching is enabled.
    /// </summary>
    internal class CachedEncryptionSession : IEncryptionSession
    {
        private readonly EncryptionSession _session;

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedEncryptionSession"/> class.
        /// </summary>
        /// <param name="session">The underlying encryption session to delegate to.</param>
        internal CachedEncryptionSession(EncryptionSession session)
        {
            _session = session;
        }

        /// <inheritdoc/>
        public byte[] Encrypt(byte[] payload)
        {
            return _session.Encrypt(payload);
        }

        /// <inheritdoc/>
        public Task<byte[]> EncryptAsync(byte[] payload)
        {
            return _session.EncryptAsync(payload);
        }

        /// <inheritdoc/>
        public byte[] Decrypt(byte[] dataRowRecord)
        {
            return _session.Decrypt(dataRowRecord);
        }

        /// <inheritdoc/>
        public Task<byte[]> DecryptAsync(byte[] dataRowRecord)
        {
            return _session.DecryptAsync(dataRowRecord);
        }

        /// <summary>
        /// No-op. Cached sessions are managed by the <see cref="SessionFactory"/> and
        /// will be disposed when the factory is disposed.
        /// </summary>
        public void Dispose()
        {
            // No-op: The underlying session is owned by the SessionFactory
        }

        /// <summary>
        /// Disposes the underlying session. Called by <see cref="SessionFactory"/> during disposal.
        /// </summary>
        internal void DisposeUnderlying()
        {
            _session.Dispose();
        }
    }
}
