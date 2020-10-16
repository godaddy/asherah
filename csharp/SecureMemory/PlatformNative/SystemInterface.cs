using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows;
using GoDaddy.Asherah.PlatformNative.LP64.Linux;
using GoDaddy.Asherah.PlatformNative.LP64.MacOS;

[assembly: InternalsVisibleTo("SecureMemory.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.PlatformNative
{
    public abstract class SystemInterface
    {
        private static readonly SystemInterface Interface;

        [ExcludeFromCodeCoverage]
        static SystemInterface()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Interface = new LinuxSystemInterfaceImpl();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Interface = new MacOSSystemInterfaceImpl();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Interface = new WindowsSystemInterfaceImpl();
            }
        }

        protected SystemInterface()
        {
            PageSize = Environment.SystemPageSize;
        }

        public int PageSize { get; }

        [ExcludeFromCodeCoverage]
        public static SystemInterface GetInstance()
        {
            if (Interface == null)
            {
                throw new PlatformNotSupportedException("Unknown platform");
            }

            return Interface;
        }

        public abstract void CopyMemory(IntPtr source, IntPtr dest, ulong length);

        public abstract void ZeroMemory(IntPtr ptr, ulong length);

        public abstract bool AreCoreDumpsGloballyDisabled();

        public abstract bool DisableCoreDumpGlobally();

        public abstract void SetNoAccess(IntPtr pointer, ulong length);

        public abstract void SetReadAccess(IntPtr pointer, ulong length);

        public abstract void SetReadWriteAccess(IntPtr pointer, ulong length);

        public abstract void SetNoDump(IntPtr protectedMemory, ulong length);

        public abstract IntPtr PageAlloc(ulong length);

        public abstract void PageFree(IntPtr pointer, ulong length);

        public abstract void LockMemory(IntPtr pointer, ulong length);

        public abstract void UnlockMemory(IntPtr pointer, ulong length);

        public abstract ulong GetMemoryLockLimit();

        public abstract void SetMemoryLockLimit(ulong limit);

        public abstract ulong GetEncryptedMemoryBlockSize();

        public abstract void ProcessEncryptMemory(IntPtr pointer, ulong length);

        public abstract void ProcessDecryptMemory(IntPtr pointer, ulong length);
    }
}
