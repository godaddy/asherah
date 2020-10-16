using System;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
namespace GoDaddy.Asherah.PlatformNative.LP64.Linux.Enums
{
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define MCL_CURRENT    1    /* Lock all currently mapped pages.  */
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define MCL_FUTURE     2    /* Lock all additions to address
    // /usr/include/x86_64-linux-gnu/bits/mman-linux.h:# define MCL_ONFAULT    4    /* Lock all pages that are faulted in */
    [Flags]
    public enum MlockFlags
    {
        MCL_CURRENT = 1,
        MCL_FUTURE = 2,
        MCL_ONFAULT = 4,
    }
}
