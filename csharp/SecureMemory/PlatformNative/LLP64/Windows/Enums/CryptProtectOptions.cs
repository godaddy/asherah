using System;

namespace GoDaddy.Asherah.PlatformNative.LLP64.Windows.Enums
{
  [Flags]
  public enum CryptProtectMemoryOptions : uint
  {
    SAME_PROCESS = 0x00,
    CROSS_PROCESS = 0x01,
    SAME_LOGON = 0x02,
  }
}
