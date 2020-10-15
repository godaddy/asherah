using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Configuration;

[assembly: InternalsVisibleTo("SecureMemory.Tests")]

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl
{
    internal class ProtectedMemorySecret : Secret
    {
        private readonly ReaderWriterLockSlim accessLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private readonly ulong length;
        private readonly IProtectedMemoryAllocator allocator;
        private readonly IConfiguration configuration;
        private readonly bool requireSecretDisposal;
        private IntPtr pointer;
        private string creationStackTrace;

        // IMPORTANT: accessCounter is not volatile nor atomic since we use accessLock for all read and write
        // access. If that changes, update the counter accordingly!
        private long accessCounter = 0;

        internal ProtectedMemorySecret(byte[] sourceBytes, IProtectedMemoryAllocator allocator, IConfiguration configuration)
        {
            Debug.WriteLine("ProtectedMemorySecret ctor");
            this.allocator = allocator;
            this.configuration = configuration;

            if (configuration != null)
            {
                if (configuration["debugSecrets"] == "true")
                {
                    creationStackTrace = Environment.StackTrace;
                }

                if (configuration["requireSecretDisposal"] == "true")
                {
                    requireSecretDisposal = true;
                }
            }

            length = (ulong)sourceBytes.Length;
            pointer = this.allocator.Alloc((ulong)sourceBytes.Length);

            if (pointer == IntPtr.Zero)
            {
                throw new ProtectedMemoryAllocationFailedException("Protected memory allocation failed");
            }

            try
            {
                Marshal.Copy(sourceBytes, 0, pointer, (int)length);
                this.allocator.SetNoAccess(pointer, length);
            }
            catch
            {
                try
                {
                    this.allocator.SetReadWriteAccess(pointer, length);
                }
                finally
                {
                    this.allocator.Free(pointer, length);
                }

                throw;
            }

            // Only clear the client's source buffer if we're successful
            SecureZeroMemory(sourceBytes);
        }

        ~ProtectedMemorySecret()
        {
            Debug.WriteLine($"ProtectedMemorySecret: Finalizer");
            Dispose(disposing: false);
        }

        public override TResult WithSecretBytes<TResult>(Func<byte[], TResult> funcWithSecret)
        {
            if (pointer == IntPtr.Zero)
            {
                throw new InvalidOperationException("Attempt to access disposed secret");
            }

            // Defend against truncation with Marshal.Copy below
            if (length > int.MaxValue)
            {
                throw new InvalidOperationException($"WithSecretBytes only supports secrets up to {int.MaxValue} bytes");
            }

            byte[] bytes = new byte[length];
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                accessLock.EnterReadLock();
                try
                {
                    SetReadAccessIfNeeded();
                    try
                    {
                        Marshal.Copy(pointer, bytes, 0, (int)length);
                    }
                    finally
                    {
                        SetNoAccessIfNeeded();
                    }
                }
                finally
                {
                    accessLock.ExitReadLock();
                }

                return funcWithSecret(bytes);
            }
            finally
            {
                SecureZeroMemory(bytes);
                handle.Free();
            }
        }

        public override TResult WithSecretUtf8Chars<TResult>(Func<char[], TResult> funcWithSecret)
        {
            if (pointer == IntPtr.Zero)
            {
                throw new InvalidOperationException("Attempt to access disposed secret");
            }

            return WithSecretBytes(bytes =>
            {
                char[] chars = Encoding.UTF8.GetChars(bytes);
                var handle = GCHandle.Alloc(chars, GCHandleType.Pinned);
                try
                {
                    return funcWithSecret(chars);
                }
                finally
                {
                    SecureZeroMemory(chars);
                    handle.Free();
                }
            });
        }

        public override TResult WithSecretIntPtr<TResult>(Func<IntPtr, ulong, TResult> funcWithSecret)
        {
            if (pointer == IntPtr.Zero)
            {
                throw new InvalidOperationException("Attempt to access disposed secret");
            }

            accessLock.EnterReadLock();
            try
            {
                SetReadAccessIfNeeded();
                try
                {
                    return funcWithSecret(pointer, length);
                }
                finally
                {
                    SetNoAccessIfNeeded();
                }
            }
            finally
            {
                accessLock.ExitReadLock();
            }
        }

        public override Secret CopySecret()
        {
            Debug.WriteLine("ProtectedMemorySecret.CopySecret");
            return WithSecretBytes(bytes => new ProtectedMemorySecret(bytes, allocator, configuration));
        }

        public override void Close()
        {
            Debug.WriteLine("ProtectedMemorySecret.Close");
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public override void Dispose()
        {
            Debug.WriteLine("ProtectedMemorySecret.Dispose");
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal static ProtectedMemorySecret FromCharArray(char[] sourceChars, IProtectedMemoryAllocator allocator, IConfiguration configuration)
        {
            byte[] sourceBytes = Encoding.UTF8.GetBytes(sourceChars);
            try
            {
                return new ProtectedMemorySecret(sourceBytes, allocator, configuration);
            }
            finally
            {
                SecureZeroMemory(sourceBytes);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (pointer == IntPtr.Zero)
            {
                return;
            }

            if (!disposing)
            {
                if (requireSecretDisposal)
                {
                    const string exceptionMessage = "FATAL: Reached finalizer for ProtectedMemorySecret (missing Dispose())";
                    throw new Exception(exceptionMessage + ((creationStackTrace == null) ? string.Empty : Environment.NewLine + creationStackTrace));
                }

                const string warningMessage = "WARN: Reached finalizer for ProtectedMemorySecret (missing Dispose())";
                Debug.WriteLine(warningMessage + ((creationStackTrace == null) ? string.Empty : Environment.NewLine + creationStackTrace));
            }
            else
            {
                accessLock.EnterWriteLock();
                try
                {
#if DEBUG
                    // TODO Add/uncomment this when we refactor logging to use static creation
                    // log.LogDebug("closing: {pointer}", ptr);
#endif
                    try
                    {
                        allocator.SetReadWriteAccess(pointer, length);
                    }
                    finally
                    {
                        allocator.Free(pointer, length);
                        pointer = IntPtr.Zero;
                    }
                }
                finally
                {
                    accessLock.ExitWriteLock();
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void SecureZeroMemory(byte[] buffer)
        {
            // NoOptimize to prevent the optimizer from deciding this call is unnecessary
            // NoInlining to prevent the inliner from forgetting that the method was no-optimize
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = 0;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void SecureZeroMemory(char[] buffer)
        {
            // NoOptimize to prevent the optimizer from deciding this call is unnecessary
            // NoInlining to prevent the inliner from forgetting that the method was no-optimize
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = '\0';
            }
        }

        private void SetReadAccessIfNeeded()
        {
            // Only set read access if we're the first one trying to access this potentially-shared Secret
            if (Interlocked.Increment(ref accessCounter) == 1)
            {
                allocator.SetReadAccess(pointer, length);
            }
        }

        private void SetNoAccessIfNeeded()
        {
            // Only set no access if we're the last one trying to access this potentially-shared Secret
            if (Interlocked.Decrement(ref accessCounter) == 0)
            {
                allocator.SetNoAccess(pointer, length);
            }
        }
    }
}
