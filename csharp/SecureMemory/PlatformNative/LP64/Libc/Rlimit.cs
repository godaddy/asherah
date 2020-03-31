using System.Diagnostics.CodeAnalysis;

// Configure types for LP64
using rlim_t = System.UInt64;

// ReSharper disable NotAccessedField.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
namespace GoDaddy.Asherah.PlatformNative.LP64.Libc
{
    // ReSharper disable once SA1300
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1121:UseBuiltInTypeAlias", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1307:AccessibleFieldsMustBeginWithUpperCaseLetter", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1310:FieldNamesMustNotContainUnderscore", Justification = "Matching native conventions")]
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
