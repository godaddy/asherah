// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

namespace GoDaddy.Asherah.PlatformNative.LP64.MacOS.Enums
{
    // /usr/include/sys/mman.h:#define POSIX_MADV_NORMAL      0    /* [MC1] no further special treatment */
    // /usr/include/sys/mman.h:#define POSIX_MADV_RANDOM      1    /* [MC1] expect random page refs */
    // /usr/include/sys/mman.h:#define POSIX_MADV_SEQUENTIAL  2    /* [MC1] expect sequential page refs */
    // /usr/include/sys/mman.h:#define POSIX_MADV_WILLNEED    3    /* [MC1] will need these pages */
    // /usr/include/sys/mman.h:#define POSIX_MADV_DONTNEED    4    /* [MC1] dont need these pages */

    // /usr/include/sys/mman.h:#define MADV_NORMAL             POSIX_MADV_NORMAL
    // /usr/include/sys/mman.h:#define MADV_RANDOM             POSIX_MADV_RANDOM
    // /usr/include/sys/mman.h:#define MADV_SEQUENTIAL         POSIX_MADV_SEQUENTIAL
    // /usr/include/sys/mman.h:#define MADV_WILLNEED           POSIX_MADV_WILLNEED
    // /usr/include/sys/mman.h:#define MADV_DONTNEED           POSIX_MADV_DONTNEED
    // /usr/include/sys/mman.h:#define MADV_FREE               5    /* pages unneeded, discard contents */
    // /usr/include/sys/mman.h:#define MADV_ZERO_WIRED_PAGES   6    /* zero the wired pages that have not been unwired before the entry is deleted */
    // /usr/include/sys/mman.h:#define MADV_FREE_REUSABLE      7    /* pages can be reused (by anyone) */
    // /usr/include/sys/mman.h:#define MADV_FREE_REUSE         8    /* caller wants to reuse those pages */
    // /usr/include/sys/mman.h:#define MADV_CAN_REUSE          9
    // /usr/include/sys/mman.h:#define MADV_PAGEOUT            10    /* page out now (internal only) */
    public enum Madvice
    {
        POSIX_MADV_NORMAL = 0,
        POSIX_MADV_RANDOM = 1,
        POSIC_MADV_SEQUENTIAL = 2,
        POSIX_MADV_WILLNEED = 3,
        POSIX_MADV_DONTNEED = 4,

        MADV_NORMAL = 0,
        MADV_RANDOM = 1,
        MADV_SEQUENTIAL = 2,
        MADV_WILLNEED = 3,
        MADV_DONTNEED = 4,
        MADV_FREE = 5,
        MADV_ZERO_WIRED_PAGES = 6,
        MADV_FREE_REUSABLE = 7,
        MADV_FREE_REUSE = 8,
        MADV_CAN_REUSE = 9,
        MADV_PAGEOUT = 10,
    }
}
