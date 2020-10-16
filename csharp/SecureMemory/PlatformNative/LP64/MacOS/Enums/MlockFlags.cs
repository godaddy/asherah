using System;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
namespace GoDaddy.Asherah.PlatformNative.LP64.MacOS.Enums
{
    // /usr/include/sys/mman.h:#define MCL_CURRENT   0x0001    /* [ML] Lock only current memory */
    // /usr/include/sys/mman.h:#define MCL_FUTURE    0x0002    /* [ML] Lock all future memory as well */
    [Flags]
    public enum MlockFlags
    {
        MCL_CURRENT = 0x0001,
        MCL_FUTURE = 0x0002,
    }
}
