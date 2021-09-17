using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Configuration;

[assembly: InternalsVisibleTo("SecureMemory.Tests")]

namespace GoDaddy.Asherah.SecureMemory.SecureMemoryImpl
{
    internal class SecureMemorySecret : Secret
    {
        private readonly ReaderWriterLockSlim pointerLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private readonly object accessLock = new object();
        private readonly ulong length;
        private readonly ISecureMemoryAllocator allocator;
        private readonly IConfiguration configuration;
        private readonly bool requireSecretDisposal;
        private IntPtr pointer;
        private string creationStackTrace;

        // IMPORTANT: accessCounter is not volatile nor atomic since we use accessLock for all read and write
        // access. If that changes, update the counter accordingly!
        private long accessCounter = 0;

        internal SecureMemorySecret(byte[] sourceBytes, ISecureMemoryAllocator allocator, IConfiguration configuration)
        {
            Debug.WriteLine("SecureMemorySecret ctor");
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
                throw new SecureMemoryAllocationFailedException("Protected memory allocation failed");
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
                    pointer = IntPtr.Zero;
                }

                throw;
            }

            // Only clear the client's source buffer if we're successful
            SecureZeroMemory(sourceBytes);
        }

        ~SecureMemorySecret()
        {
            Debug.WriteLine($"SecureMemorySecret: Finalizer");
            Dispose(disposing: false);
        }

        public override TResult WithSecretBytes<TResult>(Func<byte[], TResult> funcWithSecret)
        {
            // Defend against truncation with Marshal.Copy below
            if (length > int.MaxValue)
            {
                throw new InvalidOperationException($"WithSecretBytes only supports secrets up to {int.MaxValue} bytes");
            }

            byte[] bytes = new byte[length];
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                pointerLock.EnterReadLock();
                try
                {
                    if (pointer == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("Attempt to access disposed secret");
                    }

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
                    pointerLock.ExitReadLock();
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
            pointerLock.EnterReadLock();
            try
            {
                if (pointer == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Attempt to access disposed secret");
                }

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
                pointerLock.ExitReadLock();
            }
        }

        public override Secret CopySecret()
        {
            Debug.WriteLine("SecureMemorySecret.CopySecret");
            return WithSecretBytes(bytes => new SecureMemorySecret(bytes, allocator, configuration));
        }

        public override void Close()
        {
            Debug.WriteLine("SecureMemorySecret.Close");
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public override void Dispose()
        {
            Debug.WriteLine("SecureMemorySecret.Dispose");
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal static SecureMemorySecret FromCharArray(char[] sourceChars, ISecureMemoryAllocator allocator, IConfiguration configuration)
        {
            byte[] sourceBytes = Encoding.UTF8.GetBytes(sourceChars);
            try
            {
                return new SecureMemorySecret(sourceBytes, allocator, configuration);
            }
            finally
            {
                SecureZeroMemory(sourceBytes);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                if (pointer == IntPtr.Zero)
                {
                    return;
                }

                if (requireSecretDisposal)
                {
                    const string exceptionMessage = "FATAL: Reached finalizer for SecureMemorySecret (missing Dispose())";
                    throw new Exception(exceptionMessage + ((creationStackTrace == null) ? string.Empty : Environment.NewLine + creationStackTrace));
                }

                const string warningMessage = "WARN: Reached finalizer for SecureMemorySecret (missing Dispose())";
                Debug.WriteLine(warningMessage + ((creationStackTrace == null) ? string.Empty : Environment.NewLine + creationStackTrace));
            }
            else
            {
                pointerLock.EnterWriteLock();
                try
                {
                    if (pointer == IntPtr.Zero)
                    {
                        return;
                    }
#if DEBUG
                    // TODO Add/uncomment this when we refactor logging to use static creation
                    // log.LogDebug("closing: {pointer}", ptr);
#endif

                    // accessLock isn't needed here since we are holding the pointer lock in write
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
                    pointerLock.ExitWriteLock();
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
            // accessLock protects concurrent readers from changing access permissions at the same time
            // while holding the pointerLock in read mode
            lock (accessLock)
            {
                // Only set read access if we're the first one trying to access this potentially-shared Secret
                accessCounter++;
                if (accessCounter == 1)
                {
                    allocator.SetReadAccess(pointer, length);
                }
            }
        }

        private void SetNoAccessIfNeeded()
        {
            // accessLock protects concurrent readers from changing access permissions at the same time
            // while holding the pointerLock in read mode
            lock (accessLock)
            {
                // Only set no access if we're the last one trying to access this potentially-shared Secret
                accessCounter--;
                if (accessCounter == 0)
                {
                    allocator.SetNoAccess(pointer, length);
                }
            }
        }
    }
}
