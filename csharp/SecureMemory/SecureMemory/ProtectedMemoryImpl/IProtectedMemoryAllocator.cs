using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SecureMemory.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl
{
    internal interface IProtectedMemoryAllocator
    {
        IntPtr Alloc(ulong length);

        void Free(IntPtr pointer, ulong length);

        void SetReadWriteAccess(IntPtr pointer, ulong len);

        void SetReadAccess(IntPtr pointer, ulong length);

        void SetNoAccess(IntPtr pointer, ulong length);

        void ZeroMemory(IntPtr pointer, ulong length);
    }
}
