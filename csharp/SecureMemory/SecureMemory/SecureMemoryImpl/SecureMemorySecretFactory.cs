using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.MacOS;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.SecureMemory.SecureMemoryImpl
{
    public class SecureMemorySecretFactory : ISecretFactory
    {
        // Detect methods should throw if they know for sure what the OS/platform is, but it isn't supported
        // Detect methods should return null if they don't know for sure what the OS/platform is
        private static ISecureMemoryAllocator allocator;
        private static int refCount = 0;
        private static object allocatorLock = new object();
        private readonly IConfiguration configuration;

        public SecureMemorySecretFactory(IConfiguration configuration)
        {
            Debug.WriteLine("SecureMemoryImplSecretFactory ctor");
            lock (allocatorLock)
            {
                this.configuration = configuration;
                if (allocator != null)
                {
                    refCount++;
                    Debug.WriteLine($"SecureMemoryImplSecretFactory: Using existing allocator refCount: {refCount}");
                    return;
                }

                allocator = DetectViaRuntimeInformation(configuration)
                         ?? DetectViaOsVersionPlatform(configuration)
                         ?? DetectOsDescription(configuration);

                if (allocator == null)
                {
                    throw new PlatformNotSupportedException("Could not detect supported platform for protected memory");
                }

                Debug.WriteLine("SecureMemoryImplSecretFactory: Created new allocator");
                refCount++;
                Debug.WriteLine($"SecureMemoryImplSecretFactory: Using new allocator refCount: {refCount}");
            }
        }

        public Secret CreateSecret(byte[] secretData)
        {
            return new SecureMemorySecret(secretData, allocator, configuration);
        }

        public Secret CreateSecret(char[] secretData)
        {
            return SecureMemorySecret.FromCharArray(secretData, allocator, configuration);
        }

        public void Dispose()
        {
            Debug.WriteLine("SecureMemoryImplSecretFactory: Dispose");
            lock (allocatorLock)
            {
                if (allocator == null)
                {
                    throw new Exception("SecureMemoryImplSecretFactory.Dispose: Allocator is null!");
                }

                Debug.WriteLine("SecureMemoryImplSecretFactory: Allocator is not null");
                refCount--;
                if (refCount == 0)
                {
                    Debug.WriteLine("SecureMemoryImplSecretFactory: refCount is zero, disposing");
                    allocator.Dispose();
                    Debug.WriteLine("SecureMemoryImplSecretFactory: Setting allocator to null");
                    allocator = null;
                }
                else
                {
                    Debug.WriteLine($"SecureMemoryImplSecretFactory: New refCount is {refCount}");
                }
            }
        }

        [ExcludeFromCodeCoverage]
        private static ISecureMemoryAllocator DetectViaOsVersionPlatform(IConfiguration configuration)
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.MacOSX:
                    if (Environment.Is64BitProcess)
                    {
                        return new MacOSSecureMemoryAllocatorLP64();
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
        private static ISecureMemoryAllocator DetectViaRuntimeInformation(IConfiguration configuration)
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

                        return new MacOSSecureMemoryAllocatorLP64();
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

                        throw new PlatformNotSupportedException("Unknown secureHeapEngine: " + secureHeapEngine);
                    }
                }
            }

            // We return null if we don't know what the OS is, so other methods can be tried
            return null;
        }

        private static ISecureMemoryAllocator ConfigureForLinux64(IConfiguration configuration)
        {
            if (configuration != null)
            {
                var secureHeapEngine = configuration["secureHeapEngine"];
                if (!string.IsNullOrWhiteSpace(secureHeapEngine))
                {
                    if (string.Compare(secureHeapEngine, "mmap", StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        return new LinuxSecureMemoryAllocatorLP64();
                    }

                    throw new PlatformNotSupportedException("Unknown secureHeapEngine: " + secureHeapEngine);
                }
            }

            return new LinuxSecureMemoryAllocatorLP64();
        }

        [ExcludeFromCodeCoverage]
        private static ISecureMemoryAllocator DetectOsDescription(IConfiguration configuration)
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
                    return new MacOSSecureMemoryAllocatorLP64();
                }
            }

            // We return null if we don't know what the OS is, so other methods can be tried
            return null;
        }
    }
}
