using System;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.MacOS;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Windows;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.MacOS;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.SecureMemory
{
    public class SecureMemorySecretFactory : ISecretFactory
    {
        // Detect methods should throw if they know for sure what the OS/platform is, but it isn't supported
        // Detect methods should return null if they don't know for sure what the OS/platform is
        private static ISecureMemoryAllocator allocator;
        private static int refCount;
        private static readonly object allocatorLock = new object();
        private readonly IConfiguration configuration;

        public SecureMemorySecretFactory(IConfiguration configuration)
        {
            Debug.WriteLine("ProtectedMemorySecretFactory ctor");
            lock (allocatorLock)
            {
                this.configuration = configuration;
                if (allocator != null)
                {
                    refCount++;
                    Debug.WriteLine($"ProtectedMemorySecretFactory: Using existing allocator refCount: {refCount}");
                    return;
                }

                allocator = DetectViaRuntimeInformation(configuration)
                         ?? DetectViaOsVersionPlatform()
                         ?? DetectOsDescription(configuration);

                if (allocator == null)
                {
                    throw new PlatformNotSupportedException("Could not detect supported platform for protected memory");
                }

                Debug.WriteLine("ProtectedMemorySecretFactory: Created new allocator");
                refCount++;
                Debug.WriteLine($"ProtectedMemorySecretFactory: Using new allocator refCount: {refCount}");
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
            Debug.WriteLine("ProtectedMemorySecretFactory: Dispose");
            lock (allocatorLock)
            {
                if (allocator == null)
                {
                    throw new SecureMemoryException("ProtectedMemorySecretFactory.Dispose: Allocator is null!");
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
            GC.SuppressFinalize(this);
        }

        internal static ISecureMemoryAllocator ConfigureForMacOS64(IConfiguration configuration)
        {
            if (configuration != null)
            {
                var secureHeapEngine = configuration["secureHeapEngine"];
                var mLock = configuration["mlock"];
                if (!string.IsNullOrWhiteSpace(secureHeapEngine))
                {
                    if (string.Equals(secureHeapEngine, "openssl11", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new PlatformNotSupportedException(
                            "OpenSSL 1.1 selected for secureHeapEngine but is not yet supported for MacOS");
                    }

                    if (string.Equals(secureHeapEngine, "mmap", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(mLock))
                        {
                            if (string.Equals(mLock, "disabled", StringComparison.OrdinalIgnoreCase))
                            {
                                return new MacOSSecureMemoryAllocatorLP64();
                            }

                            throw new ConfigurationErrorsException("Unknown mlock configuration: " + mLock);
                        }

                        return new MacOSProtectedMemoryAllocatorLP64();
                    }

                    throw new PlatformNotSupportedException("Unknown secureHeapEngine: " + secureHeapEngine);
                }

                if (!string.IsNullOrWhiteSpace(mLock))
                {
                    if (string.Equals(mLock, "disabled", StringComparison.OrdinalIgnoreCase))
                    {
                        return new MacOSSecureMemoryAllocatorLP64();
                    }

                    throw new ConfigurationErrorsException("Unknown mlock configuration: " + mLock);
                }
            }

            return new MacOSProtectedMemoryAllocatorLP64();
        }

        internal static ISecureMemoryAllocator ConfigureForLinux64(IConfiguration configuration)
        {
            if (configuration != null)
            {
                var secureHeapEngine = configuration["secureHeapEngine"];
                var mLock = configuration["mlock"];
                if (!string.IsNullOrWhiteSpace(secureHeapEngine))
                {
                    if (string.Equals(secureHeapEngine, "openssl11", StringComparison.OrdinalIgnoreCase))
                    {
                        if (LinuxOpenSSL11ProtectedMemoryAllocatorLP64.IsAvailable())
                        {
                            return new LinuxOpenSSL11ProtectedMemoryAllocatorLP64(configuration);
                        }

                        throw new PlatformNotSupportedException(
                            "OpenSSL 1.1 selected for secureHeapEngine but library not found");
                    }

                    if (string.Equals(secureHeapEngine, "mmap", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(mLock))
                        {
                            if (string.Equals(mLock, "disabled", StringComparison.OrdinalIgnoreCase))
                            {
                                return new LinuxSecureMemoryAllocatorLP64();
                            }

                            throw new ConfigurationErrorsException("Unknown mlock configuration: " + mLock);
                        }

                        return new LinuxProtectedMemoryAllocatorLP64();
                    }

                    throw new PlatformNotSupportedException("Unknown secureHeapEngine: " + secureHeapEngine);
                }

                if (!string.IsNullOrWhiteSpace(mLock))
                {
                    if (string.Equals(mLock, "disabled", StringComparison.OrdinalIgnoreCase))
                    {
                        return new LinuxSecureMemoryAllocatorLP64();
                    }

                    throw new ConfigurationErrorsException("Unknown mlock configuration: " + mLock);
                }
            }

            return new LinuxProtectedMemoryAllocatorLP64();
        }

        [ExcludeFromCodeCoverage]
        [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Method returns different concrete types based on platform detection, requiring interface type for polymorphic behavior")]
        private static ISecureMemoryAllocator DetectViaOsVersionPlatform()
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
                        throw new PlatformNotSupportedException($"Unsupported architecture: {RuntimeInformation.OSArchitecture}");
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

                        return ConfigureForMacOS64(configuration);
                    case Architecture.X86:
                        throw new PlatformNotSupportedException("Unsupported architecture macOS X86");
                    case Architecture.Arm:
                        throw new PlatformNotSupportedException("Unsupported architecture macOS ARM");
                    default:
                        throw new PlatformNotSupportedException($"Unsupported architecture: {RuntimeInformation.OSArchitecture}");
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (configuration != null)
                {
                    var secureHeapEngine = configuration["secureHeapEngine"];
                    if (!string.IsNullOrWhiteSpace(secureHeapEngine))
                    {
                        if (string.Equals(secureHeapEngine, "openssl11", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new PlatformNotSupportedException(
                                "OpenSSL 1.1 selected for secureHeapEngine but not supported on Windows");
                        }

                        if (string.Equals(secureHeapEngine, "mmap", StringComparison.OrdinalIgnoreCase))
                        {
                            return new WindowsProtectedMemoryAllocatorVirtualAlloc(configuration);
                        }

                        throw new PlatformNotSupportedException("Unknown secureHeapEngine: " + secureHeapEngine);
                    }
                }

                return new WindowsProtectedMemoryAllocatorVirtualAlloc(configuration);
            }

            // We return null if we don't know what the OS is, so other methods can be tried
            return null;
        }

        [ExcludeFromCodeCoverage]
        [SuppressMessage("Usage", "CA2249:Use string.Contains instead of string.IndexOf to improve readability", Justification = "string.Contains with StringComparison is not available in netstandard2.0")]
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
                    return new MacOSProtectedMemoryAllocatorLP64();
                }
            }

            // We return null if we don't know what the OS is, so other methods can be tried
            return null;
        }
    }
}
