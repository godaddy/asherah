// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

namespace GoDaddy.Asherah.PlatformNative.LP64.Linux.Enums
{
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define POSIX_MADV_NORMAL      0 /* No further special treatment.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define POSIX_MADV_RANDOM      1 /* Expect random page references.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define POSIX_MADV_SEQUENTIAL  2 /* Expect sequential page references.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define POSIX_MADV_WILLNEED    3 /* Will need these pages.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define POSIX_MADV_DONTNEED    4 /* Don't need these pages.  */

    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define MADV_NORMAL      0    /* No further special treatment.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define MADV_RANDOM      1    /* Expect random page references.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define MADV_SEQUENTIAL  2    /* Expect sequential page references.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define MADV_WILLNEED    3    /* Will need these pages.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define MADV_DONTNEED    4    /* Don't need these pages.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define MADV_REMOVE      9    /* Remove these pages and resources.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define MADV_DONTFORK    10    /* Do not inherit across fork.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define MADV_DOFORK      11    /* Do inherit across fork.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define MADV_MERGEABLE   12    /* KSM may merge identical pages.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define MADV_UNMERGEABLE 13    /* KSM may not merge identical pages.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define MADV_HUGEPAGE    14    /* Worth backing with hugepages.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define MADV_NOHUGEPAGE  15    /* Not worth backing with hugepages.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define MADV_DONTDUMP    16    /* Explicity exclude from the core dump,
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define MADV_DODUMP      17    /* Clear the MADV_DONTDUMP flag.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define MADV_HWPOISON    100    /* Poison a page for testing.  */
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
        MADV_REMOVE = 9,
        MADV_DONTFORK = 10,
        MADV_DOFORK = 11,
        MADV_MERGEABLE = 12,
        MADV_UNMERGEABLE = 13,
        MADV_HUGEPAGE = 14,
        MADV_NOHUGEPAGE = 15,
        MADV_DONTDUMP = 16,
        MADV_DODUMP = 17,
        MADV_HWPOISON = 100,
    }
}
