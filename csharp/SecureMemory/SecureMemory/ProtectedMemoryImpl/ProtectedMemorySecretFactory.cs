using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.MacOS;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl
{
    public class ProtectedMemorySecretFactory : ISecretFactory
    {
        // Detect methods should throw if they know for sure what the OS/platform is, but it isn't supported
        // Detect methods should return null if they don't know for sure what the OS/platform is
        private static IProtectedMemoryAllocator allocator;
        private static int refCount = 0;
        private static object allocatorLock = new object();

        public ProtectedMemorySecretFactory()
        {
            GetAllocator();
        }

        public Secret CreateSecret(byte[] secretData)
        {
            return new ProtectedMemorySecret(secretData, allocator);
        }

        public Secret CreateSecret(char[] secretData)
        {
            return ProtectedMemorySecret.FromCharArray(secretData, allocator);
        }

        public void Dispose()
        {
            Console.WriteLine("ProtectedMemorySecretFactory: Dispose");
            lock (allocatorLock)
            {
                if (allocator != null)
                {
                    Console.WriteLine("ProtectedMemorySecretFactory: Allocator is not null");
                    refCount--;
                    if (refCount == 0)
                    {
                        Console.WriteLine("ProtectedMemorySecretFactory: refCount is zero, disposing");
                        allocator.Dispose();
                        Console.WriteLine("ProtectedMemorySecretFactory: Setting allocator to null");
                        allocator = null;
                    }
                    else
                    {
                        Console.WriteLine($"ProtectedMemorySecretFactory: New refCount is {refCount}");
                    }
                }
            }
        }

        internal static IProtectedMemoryAllocator GetAllocator()
        {
            lock (allocatorLock)
            {
                if (allocator != null)
                {
                    refCount++;
                    Console.WriteLine($"ProtectedMemorySecretFactory: Using existing allocator refCount: {refCount}");
                    return allocator;
                }

                allocator = DetectViaRuntimeInformation()
                         ?? DetectViaOsVersionPlatform()
                         ?? DetectOsDescription();

                if (allocator == null)
                {
                    throw new PlatformNotSupportedException("Could not detect supported platform for protected memory");
                }

                refCount++;
                Console.WriteLine($"ProtectedMemorySecretFactory: Using new allocator refCount: {refCount}");
                return allocator;
            }
        }

        [ExcludeFromCodeCoverage]
        private static IProtectedMemoryAllocator DetectViaOsVersionPlatform()
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.MacOSX:
                    if (Environment.Is64BitProcess)
                    {
                        return new MacOSProtectedMemoryAllocatorLP64();
                    }

                    throw new PlatformNotSupportedException("Non-64bit process on macOS not supported");
                case PlatformID.Win32NT:
                    throw new PlatformNotSupportedException("PlatformID.Win32NT is not supported");
                case PlatformID.Unix:
                    // Unix is something of a non-answer since this could be:
                    // Linux, macOS (despite PlatformID.MacOSX), FreeBSD, or something else
                    return null;
            }

            // We return null if we don't know what the OS is, so other methods can be tried
            return null;
        }

        [ExcludeFromCodeCoverage]
        private static IProtectedMemoryAllocator DetectViaRuntimeInformation()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                switch (RuntimeInformation.OSArchitecture)
                {
                    case Architecture.X64:
                    case Architecture.Arm64:
                        if (Environment.Is64BitProcess == false)
                        {
                            throw new PlatformNotSupportedException("Non-64bit process not supported on Linux X64 or Aarch64");
                        }

                        if (LinuxOpenSSL11ProtectedMemoryAllocatorLP64.IsAvailable())
                        {
                            return new LinuxOpenSSL11ProtectedMemoryAllocatorLP64(8388608, 128);
                        }

                        return new LinuxProtectedMemoryAllocatorLP64();
                    case Architecture.X86:
                        throw new PlatformNotSupportedException("Unsupported architecture Linux X86");
                    case Architecture.Arm:
                        throw new PlatformNotSupportedException("Unsupported architecture Linux ARM");
                    default:
                        throw new ArgumentOutOfRangeException("Unknown OSArchitecture");
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                switch (RuntimeInformation.OSArchitecture)
                {
                    case Architecture.X64:
                    case Architecture.Arm64:
                        if (Environment.Is64BitProcess == false)
                        {
                            throw new PlatformNotSupportedException("Non-64bit process not supported on macOS X64 or Arm64");
                        }

                        return new MacOSProtectedMemoryAllocatorLP64();
                    case Architecture.X86:
                        throw new PlatformNotSupportedException("Unsupported architecture macOS X86");
                    case Architecture.Arm:
                        throw new PlatformNotSupportedException("Unsupported architecture macOS ARM");
                    default:
                        throw new ArgumentOutOfRangeException("Unknown OSArchitecture");
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsProtectedMemoryAllocatorVirtualAlloc();
            }

            // We return null if we don't know what the OS is, so other methods can be tried
            return null;
        }

        [ExcludeFromCodeCoverage]
        private static IProtectedMemoryAllocator DetectOsDescription()
        {
            var desc = RuntimeInformation.OSDescription;
            if (desc.IndexOf("Linux", StringComparison.OrdinalIgnoreCase) != -1)
            {
                if (Environment.Is64BitProcess)
                {
                    return new LinuxProtectedMemoryAllocatorLP64();
                }

                if (desc.IndexOf("i686", StringComparison.OrdinalIgnoreCase) == -1)
                {
                    throw new PlatformNotSupportedException("Linux i686 not supported yet");
                }
            }
            else if (desc.IndexOf("Darwin", StringComparison.OrdinalIgnoreCase) != -1)
            {
                if (Environment.Is64BitProcess)
                {
                    return new MacOSProtectedMemoryAllocatorLP64();
                }
            }

            // We return null if we don't know what the OS is, so other methods can be tried
            return null;
        }
    }
}
