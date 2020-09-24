using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

[assembly: InternalsVisibleTo("SecureMemory.Tests")]

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl
{
    internal class ProtectedMemorySecret : Secret
    {
        private readonly object accessLock = new object();

        private readonly ulong length;
        private readonly IProtectedMemoryAllocator allocator;
        private IntPtr pointer;

        // IMPORTANT: accessCounter is not volatile nor atomic since we use accessLock for all read and write
        // access. If that changes, update the counter accordingly!
        private long accessCounter = 0;

        internal ProtectedMemorySecret(byte[] sourceBytes, IProtectedMemoryAllocator allocator)
        {
            Debug.WriteLine("ProtectedMemorySecret ctor");
            this.allocator = allocator;

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
                // Shouldn't happen, but need to free memory to avoid leak if it does
                IntPtr oldPtr = Interlocked.Exchange(ref pointer, IntPtr.Zero);
                if (oldPtr != IntPtr.Zero)
                {
                    this.allocator.SetReadWriteAccess(oldPtr, length);
                    this.allocator.Free(oldPtr, length);
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

            byte[] bytes = new byte[length];
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

                return funcWithSecret(bytes);
            }
            finally
            {
                SecureZeroMemory(bytes);
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
                try
                {
                    return funcWithSecret(chars);
                }
                finally
                {
                    SecureZeroMemory(chars);
                }
            });
        }

        public override Secret CopySecret()
        {
            Debug.WriteLine("ProtectedMemorySecret.CopySecret");
            return WithSecretBytes(bytes => new ProtectedMemorySecret(bytes, allocator));
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

        internal static ProtectedMemorySecret FromCharArray(char[] sourceChars, IProtectedMemoryAllocator allocator)
        {
            byte[] sourceBytes = Encoding.UTF8.GetBytes(sourceChars);
            try
            {
                return new ProtectedMemorySecret(sourceBytes, allocator);
            }
            finally
            {
                SecureZeroMemory(sourceBytes);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (pointer != IntPtr.Zero)
            {
                if (!disposing)
                {
                    throw new Exception("FATAL: Reached finalizer for ProtectedMemorySecret (missing Dispose())");
                }
                else
                {
                    Release(allocator, ref pointer, length);
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

        private static void Release(IProtectedMemoryAllocator pm, ref IntPtr ptr, ulong len)
        {
#if DEBUG
            // TODO Add/uncomment this when we refactor logging to use static creation
            // log.LogDebug("closing: {pointer}", ptr);
#endif
            IntPtr oldPtr = Interlocked.Exchange(ref ptr, IntPtr.Zero);
            if (oldPtr != IntPtr.Zero)
            {
                try
                {
                    pm.SetReadWriteAccess(oldPtr, len);
                }
                finally
                {
                    pm.Free(oldPtr, len);
                }
            }
        }

        // IMPORTANT: lock() does not guarantee fairness and is vulnerable to thread starvation
        private void SetReadAccessIfNeeded()
        {
            lock (accessLock)
            {
                // Only set read access if we're the first one trying to access this potentially-shared Secret
                if (accessCounter == 0)
                {
                    allocator.SetReadAccess(pointer, length);
                }

                accessCounter++;
            }
        }

        // IMPORTANT: lock() does not guarantee fairness and is vulnerable to thread starvation
        private void SetNoAccessIfNeeded()
        {
            lock (accessLock)
            {
                accessCounter--;

                // Only set no access if we're the last one trying to access this potentially-shared Secret
                if (accessCounter == 0)
                {
                    allocator.SetNoAccess(pointer, length);
                }
            }
        }
    }
}
