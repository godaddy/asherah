using System;
using System.Runtime.CompilerServices;
using GoDaddy.Asherah.PlatformNative.LP64.Libc;
using GoDaddy.Asherah.SecureMemory.Libc;

[assembly: InternalsVisibleTo("SecureMemory.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Libc
{
    internal abstract class LibcSecureMemoryAllocatorLP64 : LibcMemoryAllocatorLP64
    {
        // ************************************
        // alloc / free
        // ************************************
        public override IntPtr Alloc(ulong length)
        {
            // Some platforms may require fd to be -1 even if using anonymous
            var secureMemory = LibcLP64.mmap(
                IntPtr.Zero, length, GetProtReadWrite(), GetPrivateAnonymousFlags(), -1, 0);

            Check.ValidatePointer(secureMemory, "mmap");
            SetNoDump(secureMemory, length);

            return secureMemory;
        }

        public override void Free(IntPtr pointer, ulong length)
        {
            try
            {
                // Wipe the protected memory (assumes memory was made writeable)
                ZeroMemory(pointer, length);
            }
            finally
            {
                // Free (unmap) the protected memory
                Check.Zero(LibcLP64.munmap(pointer, length), "munmap");
            }
        }
    }
}
