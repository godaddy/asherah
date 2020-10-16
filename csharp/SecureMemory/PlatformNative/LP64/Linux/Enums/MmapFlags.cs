using System;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
namespace GoDaddy.Asherah.PlatformNative.LP64.Linux.Enums
{
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h: #define  MAP_SHARED     0x01               /* Share changes.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h: #define  MAP_PRIVATE    0x02               /* Changes are private.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h: #define  MAP_TYPE       0x0f               /* Mask for type of mapping.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h: #define  MAP_FIXED      0x10               /* Interpret addr exactly.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h: #define  MAP_FILE       0
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h: #define  MAP_ANONYMOUS  __MAP_ANONYMOUS    /* Don't use a file.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h: #define  MAP_ANONYMOUS  0x20               /* Don't use a file.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h: #define  MAP_ANON       MAP_ANONYMOUS

    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:/* When MAP_HUGETLB is set bits [26:31] encode the log2 of the huge page size.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h: #define  MAP_HUGE_SHIFT 26
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h: #define  MAP_HUGE_MASK  0x3f

    // /usr/include/x86_64-linux-gnu/bits/mman.h:       #define  MAP_32BIT      0x40        /* Only give out 32-bit addresses.  */
    // /usr/include/x86_64-linux-gnu/bits/mman.h:       #define  MAP_GROWSDOWN  0x00100     /* Stack-like segment.  */
    // /usr/include/x86_64-linux-gnu/bits/mman.h:       #define  MAP_DENYWRITE  0x00800     /* ETXTBSY */
    // /usr/include/x86_64-linux-gnu/bits/mman.h:       #define  MAP_EXECUTABLE 0x01000     /* Mark it as an executable.  */
    // /usr/include/x86_64-linux-gnu/bits/mman.h:       #define  MAP_LOCKED     0x02000     /* Lock the mapping.  */
    // /usr/include/x86_64-linux-gnu/bits/mman.h:       #define  MAP_NORESERVE  0x04000     /* Don't check for reservations.  */
    // /usr/include/x86_64-linux-gnu/bits/mman.h:       #define  MAP_POPULATE   0x08000     /* Populate (prefault) pagetables.  */
    // /usr/include/x86_64-linux-gnu/bits/mman.h:       #define  MAP_NONBLOCK   0x10000     /* Do not block on IO.  */
    // /usr/include/x86_64-linux-gnu/bits/mman.h:       #define  MAP_STACK      0x20000     /* Allocation is for a stack.  */
    // /usr/include/x86_64-linux-gnu/bits/mman.h:       #define  MAP_HUGETLB    0x40000     /* Create huge page mapping.  */
    // /usr/include/x86_64-linux-gnu/sys/mman.h:        #define  MAP_FAILED     ((void *) -1)
    [Flags]
    public enum MmapFlags
    {
        MAP_SHARED = 0x01,
        MAP_PRIVATE = 0x02,
        MAP_TYPE = 0x0f,
        MAP_FIXED = 0x10,
        MAP_FILE = 0,
        MAP_ANONYMOUS = 0x20,
        MAP_ANON = 0x20,
        MAP_32BIT = 0x40,

        MAP_HUGE_SHIFT = 26,
        MAP_HUGE_MASK = 0x3f,

        MAP_GROWSDOWN = 0x00100,
        MAP_DENYWRITE = 0x00800,
        MAP_EXECUTABLE = 0x01000,
        MAP_LOCKED = 0x02000,
        MAP_NORESERVE = 0x04000,
        MAP_POPULATE = 0x08000,
        MAP_NONBLOCK = 0x10000,
        MAP_STACK = 0x20000,
        MAP_HUGETLB = 0x40000,
    }
}
