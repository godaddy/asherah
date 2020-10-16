using System;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
namespace GoDaddy.Asherah.PlatformNative.LP64.Linux.Enums
{
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:#define PROT_READ        0x1           /* Page can be read.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:#define PROT_WRITE       0x2           /* Page can be written.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:#define PROT_EXEC        0x4           /* Page can be executed.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:#define PROT_NONE        0x0           /* Page can not be accessed.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:#define PROT_GROWSDOWN   0x01000000    /* Extend change to start of
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:#define PROT_GROWSUP     0x02000000    /* Extend change to start of
    [Flags]
    public enum MmapProts
    {
        PROT_NONE = 0x0,
        PROT_READ = 0x01,
        PROT_WRITE = 0x02,
        PROT_EXEC = 0x04,
        PROT_GROWSDOWN = 0x01000000,
        PROT_GROWSUP = 0x02000000,
    }
}
