using System;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
namespace GoDaddy.Asherah.PlatformNative.LP64.MacOS.Enums
{
    // /usr/include/sys/mman.h:#define MAP_SHARED        0x0001       /* [MF|SHM] share changes */
    // /usr/include/sys/mman.h:#define MAP_PRIVATE       0x0002       /* [MF|SHM] changes are private */
    // /usr/include/sys/mman.h:#define MAP_COPY          MAP_PRIVATE  /* Obsolete */
    // /usr/include/sys/mman.h:#define MAP_FIXED         0x0010       /* [MF|SHM] interpret addr exactly */
    // /usr/include/sys/mman.h:#define MAP_RENAME        0x0020       /* Sun: rename private pages to file */
    // /usr/include/sys/mman.h:#define MAP_NORESERVE     0x0040       /* Sun: don't reserve needed swap area */
    // /usr/include/sys/mman.h:#define MAP_RESERVED0080  0x0080       /* previously unimplemented MAP_INHERIT */
    // /usr/include/sys/mman.h:#define MAP_NOEXTEND      0x0100       /* for MAP_FILE, don't change file size */
    // /usr/include/sys/mman.h:#define MAP_HASSEMAPHORE  0x0200       /* region may contain semaphores */
    // /usr/include/sys/mman.h:#define MAP_NOCACHE       0x0400       /* don't cache pages for this mapping */
    // /usr/include/sys/mman.h:#define MAP_JIT           0x0800       /* Allocate a region that will be used for JIT purposes */
    // /usr/include/sys/mman.h:#define MAP_FILE          0x0000       /* map from file (default) */
    // /usr/include/sys/mman.h:#define MAP_ANON          0x1000       /* allocated from memory, swap space */
    // /usr/include/sys/mman.h:#define MAP_ANONYMOUS     MAP_ANON
    [Flags]
    public enum MmapFlags
    {
        MAP_SHARED = 0x0001,
        MAP_PRIVATE = 0x0002,
        MAP_COPY = 0x0002, // MAP_PRIVATE
        MAP_FIXED = 0x0010,
        MAP_RENAME = 0x0020,
        MAP_NORESERVE = 0x0040,
        MAP_RESERVED0080 = 0x0080,
        MAP_NOEXTEND = 0x0100,
        MAP_HASSEMAPHORE = 0x0200,
        MAP_NOCACHE = 0x0400,
        MAP_JIT = 0x0800,
        MAP_FILE = 0x0000,
        MAP_ANON = 0x1000,
        MAP_ANONYMOUS = 0x1000, // MAP_ANON
    }
}
