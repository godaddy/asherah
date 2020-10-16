using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative.LLP64.Windows;
using GoDaddy.Asherah.PlatformNative.LP64.Linux;
using GoDaddy.Asherah.PlatformNative.LP64.MacOS;

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
                Interface = new LinuxSystemInterfaceImpl(new LinuxLibcLP64());
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Interface = new MacOSSystemInterfaceImpl(new MacOSLibcLP64());
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Interface = new WindowsSystemInterfaceImpl();
            }
        }

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
    }
}
