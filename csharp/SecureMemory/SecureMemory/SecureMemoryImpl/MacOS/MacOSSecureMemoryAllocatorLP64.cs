using System;
using System.Runtime.CompilerServices;
using GoDaddy.Asherah.PlatformNative.LP64.MacOS;
using GoDaddy.Asherah.PlatformNative.LP64.MacOS.Enums;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Libc;

[assembly: InternalsVisibleTo("SecureMemory.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.MacOS
{
  /*
   * MacOS protected memory implementation supports:
   *
   * mmap(MAP_PRIVATE | MAP_ANON) - Private anonymous
   * setrlimit(RLIMIT_CORE, 0) - Globally disable core dumps
   * madvise(MADV_ZERO_WIRED_PAGES) - Request that the pages are zeroed before deallocation
   */

  internal class MacOSSecureMemoryAllocatorLP64 : LibcSecureMemoryAllocatorLP64
  {
    public MacOSSecureMemoryAllocatorLP64()
    {
      DisableCoreDumpGlobally();
    }

    public override void Dispose()
    {
    }

    internal override int GetRlimitCoreResource()
    {
      return (int)RlimitResource.RLIMIT_CORE;
    }

    // Platform specific blocking memory from core dump
    internal override void SetNoDump(IntPtr secureMemory, ulong length)
    {
      // MacOS doesn't have madvise(MAP_DONTDUMP) so we have to disable core dumps globally
      if (!AreCoreDumpsGloballyDisabled())
      {
        DisableCoreDumpGlobally();
        if (!AreCoreDumpsGloballyDisabled())
        {
          throw new SecureMemoryException("Failed to disable core dumps");
        }
      }
    }

    // These flags are platform specific in their integer values
    internal override int GetProtRead()
    {
      return (int)MmapProts.PROT_READ;
    }

    internal override int GetProtReadWrite()
    {
      return (int)(MmapProts.PROT_READ | MmapProts.PROT_WRITE);
    }

    internal override int GetProtNoAccess()
    {
      return (int)MmapProts.PROT_NONE;
    }

    internal override int GetPrivateAnonymousFlags()
    {
      return (int)(MmapFlags.MAP_PRIVATE | MmapFlags.MAP_ANON);
    }

    protected override void ZeroMemory(IntPtr pointer, ulong length)
    {
      // This differs on different platforms
      // MacOS has memset_s which is standardized and secure
      MacOSLibcLP64.memset_s(pointer, length, 0, length);
    }
  }
}
