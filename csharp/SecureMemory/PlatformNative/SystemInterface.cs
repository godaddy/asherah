using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows;
using GoDaddy.Asherah.PlatformNative.LP64.Linux;
using GoDaddy.Asherah.PlatformNative.LP64.MacOS;
using Microsoft.Extensions.Configuration;

[assembly: InternalsVisibleTo("SecureMemory.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.PlatformNative
{
    public abstract class SystemInterface
    {
        private static readonly object SystemInterfaceLock = new object();
        private static SystemInterface systemInterface;

        protected SystemInterface()
        {
            PageSize = Environment.SystemPageSize;
        }

        public int PageSize { get; }

        [ExcludeFromCodeCoverage]
        public static SystemInterface GetExistingInstance()
        {
            lock (SystemInterfaceLock)
            {
                if (systemInterface == null)
                {
                    throw new Exception("SystemInterface not configured");
                }
            }

            return systemInterface;
        }

        [ExcludeFromCodeCoverage]
        public static SystemInterface ConfigureSystemInterface(IConfiguration configuration)
        {
            lock (SystemInterfaceLock)
            {
                if (systemInterface == null)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        systemInterface = new LinuxSystemInterfaceImpl();
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        systemInterface = new MacOSSystemInterfaceImpl();
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        systemInterface = new WindowsSystemInterfaceImpl();
                    }
                    else
                    {
                        throw new PlatformNotSupportedException("Unknown platform");
                    }
                }
            }

            return systemInterface;
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
    }
}
