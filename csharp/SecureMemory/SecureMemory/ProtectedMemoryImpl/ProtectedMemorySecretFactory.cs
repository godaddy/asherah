using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.PlatformNative;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.MacOS;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl
{
    public class ProtectedMemorySecretFactory : ISecretFactory
    {
        // Detect methods should throw if they know for sure what the OS/platform is, but it isn't supported
        // Detect methods should return null if they don't know for sure what the OS/platform is
        private static readonly object AllocatorLock = new object();
        private static IProtectedMemoryAllocator allocator;
        private static SystemInterface systemInterface;
        private static int refCount = 0;
        private readonly IConfiguration configuration;

        public ProtectedMemorySecretFactory(IConfiguration configuration)
        {
            Debug.WriteLine("ProtectedMemorySecretFactory ctor");
            lock (AllocatorLock)
            {
                this.configuration = configuration;
                if (allocator != null)
                {
                    refCount++;
                    Debug.WriteLine($"ProtectedMemorySecretFactory: Using existing allocator refCount: {refCount}");
                    return;
                }

                allocator = DetectAllocator(configuration);

                Debug.WriteLine("ProtectedMemorySecretFactory: Created new allocator");
                refCount++;
                Debug.WriteLine($"ProtectedMemorySecretFactory: Using new allocator refCount: {refCount}");

                systemInterface = SystemInterface.GetInstance();
            }
        }

        public Secret CreateSecret(IntPtr secretData, ulong length)
        {
            return new ProtectedMemorySecret(secretData, length, allocator, systemInterface, configuration);
        }

        public Secret CreateSecret(byte[] secretData)
        {
            return new ProtectedMemorySecret(secretData, allocator, systemInterface, configuration);
        }

        public Secret CreateSecret(char[] secretData)
        {
            return ProtectedMemorySecret.FromCharArray(secretData, allocator, systemInterface, configuration);
        }

        public void Dispose()
        {
            Debug.WriteLine("ProtectedMemorySecretFactory: Dispose");
            lock (AllocatorLock)
            {
                if (allocator == null)
                {
                    throw new Exception("ProtectedMemorySecretFactory.Dispose: Allocator is null!");
                }

                Debug.WriteLine("ProtectedMemorySecretFactory: Allocator is not null");
                refCount--;
                if (refCount == 0)
                {
                    Debug.WriteLine("ProtectedMemorySecretFactory: refCount is zero, disposing");
                    allocator.Dispose();
                    Debug.WriteLine("ProtectedMemorySecretFactory: Setting allocator to null");
                    allocator = null;
                }
                else
                {
                    Debug.WriteLine($"ProtectedMemorySecretFactory: New refCount is {refCount}");
                }
            }
        }

        [ExcludeFromCodeCoverage]
        private static IProtectedMemoryAllocator DetectAllocator(IConfiguration configuration)
        {
            var platformAllocator = DetectViaRuntimeInformation(configuration)
                        ?? DetectViaOsVersionPlatform()
                        ?? DetectOsDescription(configuration);

            if (platformAllocator == null)
            {
                throw new PlatformNotSupportedException("Could not detect supported platform for protected memory");
            }

            return platformAllocator;
        }

        [ExcludeFromCodeCoverage]
        private static IProtectedMemoryAllocator DetectViaOsVersionPlatform()
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.MacOSX:
                    if (Environment.Is64BitProcess)
                    {
                        return new MacOSProtectedMemoryAllocatorLP64(systemInterface);
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
        private static IProtectedMemoryAllocator DetectViaRuntimeInformation(IConfiguration configuration)
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

                        return ConfigureForLinux64(configuration);
                    case Architecture.X86:
                        throw new PlatformNotSupportedException("Unsupported architecture Linux X86");
                    case Architecture.Arm:
                        throw new PlatformNotSupportedException("Unsupported architecture Linux ARM");
                    default:
                        throw new PlatformNotSupportedException("Unknown OSArchitecture");
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

                        return new MacOSProtectedMemoryAllocatorLP64(systemInterface);
                    case Architecture.X86:
                        throw new PlatformNotSupportedException("Unsupported architecture macOS X86");
                    case Architecture.Arm:
                        throw new PlatformNotSupportedException("Unsupported architecture macOS ARM");
                    default:
                        throw new PlatformNotSupportedException("Unknown OSArchitecture");
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (configuration != null)
                {
                    var secureHeapEngine = configuration["secureHeapEngine"];
                    if (!string.IsNullOrWhiteSpace(secureHeapEngine))
                    {
                        if (string.Compare(secureHeapEngine, "openssl11", StringComparison.InvariantCultureIgnoreCase) == 0)
                        {
                            throw new PlatformNotSupportedException(
                                "OpenSSL 1.1 selected for secureHeapEngine but not supported on Windows");
                        }

                        if (string.Compare(secureHeapEngine, "mmap", StringComparison.InvariantCultureIgnoreCase) == 0)
                        {
                            return new WindowsProtectedMemoryAllocatorVirtualAlloc(configuration, systemInterface);
                        }

                        throw new PlatformNotSupportedException("Unknown secureHeapEngine: " + secureHeapEngine);
                    }
                }

                return new WindowsProtectedMemoryAllocatorVirtualAlloc(configuration, systemInterface);
            }

            // We return null if we don't know what the OS is, so other methods can be tried
            return null;
        }

        private static IProtectedMemoryAllocator ConfigureForLinux64(IConfiguration configuration)
        {
            if (configuration != null)
            {
                var secureHeapEngine = configuration["secureHeapEngine"];
                if (!string.IsNullOrWhiteSpace(secureHeapEngine))
                {
                    if (string.Compare(secureHeapEngine, "openssl11", StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        if (LinuxOpenSSL11ProtectedMemoryAllocatorLP64.IsAvailable())
                        {
                            return new LinuxOpenSSL11ProtectedMemoryAllocatorLP64(configuration, systemInterface);
                        }

                        throw new PlatformNotSupportedException(
                            "OpenSSL 1.1 selected for secureHeapEngine but library not found");
                    }

                    if (string.Compare(secureHeapEngine, "mmap", StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        return new LinuxProtectedMemoryAllocatorLP64(systemInterface);
                    }

                    throw new PlatformNotSupportedException("Unknown secureHeapEngine: " + secureHeapEngine);
                }
            }

            return new LinuxProtectedMemoryAllocatorLP64(systemInterface);
        }

        [ExcludeFromCodeCoverage]
        private static IProtectedMemoryAllocator DetectOsDescription(IConfiguration configuration)
        {
            var desc = RuntimeInformation.OSDescription;
            if (desc.IndexOf("Linux", StringComparison.OrdinalIgnoreCase) != -1)
            {
                if (Environment.Is64BitProcess)
                {
                    return ConfigureForLinux64(configuration);
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
                    return new MacOSProtectedMemoryAllocatorLP64(systemInterface);
                }
            }

            // We return null if we don't know what the OS is, so other methods can be tried
            return null;
        }
    }
}
