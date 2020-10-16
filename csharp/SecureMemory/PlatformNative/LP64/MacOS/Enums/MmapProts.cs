using System;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
namespace GoDaddy.Asherah.PlatformNative.LP64.MacOS.Enums
{
    // /usr/include/sys/mman.h:#define  PROT_NONE    0x00    /* [MC2] no permissions */
    // /usr/include/sys/mman.h:#define  PROT_READ    0x01    /* [MC2] pages can be read */
    // /usr/include/sys/mman.h:#define  PROT_WRITE   0x02    /* [MC2] pages can be written */
    // /usr/include/sys/mman.h:#define  PROT_EXEC    0x04    /* [MC2] pages can be executed */
    [Flags]
    public enum MmapProts
    {
        PROT_NONE = 0x00,
        PROT_READ = 0x01,
        PROT_WRITE = 0x02,
        PORT_EXEC = 0x04,
    }
}
