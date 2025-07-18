// Configure types for LP64
using rlim_t = System.UInt64;

namespace GoDaddy.Asherah.PlatformNative.LP64.Libc
{
  public struct rlimit
  {
    public const rlim_t UNLIMITED = ulong.MaxValue; // 0xffffffffffffffff

    public rlim_t rlim_cur;
    public rlim_t rlim_max;

    public static rlimit Zero()
    {
      return new rlimit
      {
        rlim_cur = 0,
        rlim_max = 0,
      };
    }
  }
}
