// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
namespace GoDaddy.Asherah.PlatformNative.LP64.MacOS.Enums
{
    // /usr/include/sys/resource.h:#define RLIMIT_CPU       0            /* cpu time per process */
    // /usr/include/sys/resource.h:#define RLIMIT_FSIZE     1            /* file size */
    // /usr/include/sys/resource.h:#define RLIMIT_DATA      2            /* data segment size */
    // /usr/include/sys/resource.h:#define RLIMIT_STACK     3            /* stack size */
    // /usr/include/sys/resource.h:#define RLIMIT_CORE      4            /* core file size */
    // /usr/include/sys/resource.h:#define RLIMIT_AS        5            /* address space (resident set size) */
    // /usr/include/sys/resource.h:#define RLIMIT_RSS       RLIMIT_AS    /* source compatibility alias */
    // /usr/include/sys/resource.h:#define RLIMIT_MEMLOCK   6            /* locked-in-memory address space */
    // /usr/include/sys/resource.h:#define RLIMIT_NPROC     7            /* number of processes */
    // /usr/include/sys/resource.h:#define RLIMIT_NOFILE    8            /* number of open files */
    public enum RlimitResource
    {
        RLIMIT_CPU = 0, // cpu time per process
        RLIMIT_FSIZE = 1, // file size
        RLIMIT_DATA = 2, // data segment size
        RLIMIT_STACK = 3, // stack size
        RLIMIT_CORE = 4,  // core file size
        RLIMIT_AS = 5, // address space (resident set size)
        RLIMIT_RSS = 5, // source compatibility alias
        RLIMIT_MEMLOCK = 6, // locked-in-memory address space
        RLIMIT_NPROC = 7, // number of processes
        RLIMIT_NOFILE = 8, // number of open files
    }
}
