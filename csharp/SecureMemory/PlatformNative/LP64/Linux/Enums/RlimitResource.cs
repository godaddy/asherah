// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
namespace GoDaddy.Asherah.PlatformNative.LP64.Linux.Enums
{
    // /usr/include/x86_64-linux-gnu/bits/resource.h:        RLIMIT_CPU = 0,
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_CPU RLIMIT_CPU
    // /usr/include/x86_64-linux-gnu/bits/resource.h:        RLIMIT_FSIZE = 1,
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_FSIZE RLIMIT_FSIZE
    // /usr/include/x86_64-linux-gnu/bits/resource.h:        RLIMIT_DATA = 2,
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_DATA RLIMIT_DATA
    // /usr/include/x86_64-linux-gnu/bits/resource.h:        RLIMIT_STACK = 3,
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_STACK RLIMIT_STACK
    // /usr/include/x86_64-linux-gnu/bits/resource.h:        RLIMIT_CORE = 4,
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_CORE RLIMIT_CORE
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_RSS __RLIMIT_RSS
    // /usr/include/x86_64-linux-gnu/bits/resource.h:        RLIMIT_NOFILE = 7,
    // /usr/include/x86_64-linux-gnu/bits/resource.h:      __RLIMIT_OFILE = RLIMIT_NOFILE, /* BSD name for same.  */
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_NOFILE RLIMIT_NOFILE
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_OFILE __RLIMIT_OFILE
    // /usr/include/x86_64-linux-gnu/bits/resource.h:        RLIMIT_AS = 9,
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_AS RLIMIT_AS
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_NPROC __RLIMIT_NPROC
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_MEMLOCK __RLIMIT_MEMLOCK
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_LOCKS __RLIMIT_LOCKS
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_SIGPENDING __RLIMIT_SIGPENDING
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_MSGQUEUE __RLIMIT_MSGQUEUE
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_NICE __RLIMIT_NICE
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_RTPRIO __RLIMIT_RTPRIO
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_RTTIME __RLIMIT_RTTIME
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_NLIMITS __RLIMIT_NLIMITS

    // /usr/include/x86_64-linux-gnu/bits/resource.h:      __RLIMIT_RSS = 5,
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_RSS __RLIMIT_RSS
    // /usr/include/x86_64-linux-gnu/bits/resource.h:      __RLIMIT_OFILE = RLIMIT_NOFILE, /* BSD name for same.  */
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_OFILE __RLIMIT_OFILE
    // /usr/include/x86_64-linux-gnu/bits/resource.h:      __RLIMIT_NPROC = 6,
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_NPROC __RLIMIT_NPROC
    // /usr/include/x86_64-linux-gnu/bits/resource.h:      __RLIMIT_MEMLOCK = 8,
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_MEMLOCK __RLIMIT_MEMLOCK
    // /usr/include/x86_64-linux-gnu/bits/resource.h:      __RLIMIT_LOCKS = 10,
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_LOCKS __RLIMIT_LOCKS
    // /usr/include/x86_64-linux-gnu/bits/resource.h:      __RLIMIT_SIGPENDING = 11,
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_SIGPENDING __RLIMIT_SIGPENDING
    // /usr/include/x86_64-linux-gnu/bits/resource.h:      __RLIMIT_MSGQUEUE = 12,
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_MSGQUEUE __RLIMIT_MSGQUEUE
    // /usr/include/x86_64-linux-gnu/bits/resource.h:      __RLIMIT_NICE = 13,
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_NICE __RLIMIT_NICE
    // /usr/include/x86_64-linux-gnu/bits/resource.h:      __RLIMIT_RTPRIO = 14,
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_RTPRIO __RLIMIT_RTPRIO
    // /usr/include/x86_64-linux-gnu/bits/resource.h:      __RLIMIT_RTTIME = 15,
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_RTTIME __RLIMIT_RTTIME
    // /usr/include/x86_64-linux-gnu/bits/resource.h:      __RLIMIT_NLIMITS = 16,
    // /usr/include/x86_64-linux-gnu/bits/resource.h:      __RLIM_NLIMITS = __RLIMIT_NLIMITS
    // /usr/include/x86_64-linux-gnu/bits/resource.h:#define RLIMIT_NLIMITS __RLIMIT_NLIMITS
    public enum RlimitResource
    {
        RLIMIT_CPU = 0,
        RLIMIT_FSIZE = 1,
        RLIMIT_DATA = 2,
        RLIMIT_STACK = 3,
        RLIMIT_CORE = 4,
        RLIMIT_NOFILE = 7,
        RLIMIT_AS = 9,

        RLIMIT_RSS = 5,
        RLIMIT_NPROC = 6,
        RLIMIT_MEMLOCK = 8,
        RLIMIT_LOCKS = 10,
        RLIMIT_SIGPENDING = 11,
        RLIMIT_MSGQUEUE = 12,
        RLIMIT_NICE = 13,
        RLIMIT_RTPRIO = 14,
        RLIMIT_RTTIME = 15,
        RLIMIT_NLIMITS = 16,
    }
}
