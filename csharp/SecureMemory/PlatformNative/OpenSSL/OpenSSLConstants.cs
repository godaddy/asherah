using System.Diagnostics.CodeAnalysis;

namespace GoDaddy.Asherah.PlatformNative.OpenSSL
{
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1121:UseBuiltInTypeAlias", Justification = "Matching native conventions")]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1202:ElementsMustBeOrderedByAccess", Justification = "Matching native conventions")]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Matching native conventions")]
    internal static class OpenSSLConstants
    {
        public const int EVP_CTRL_AEAD_GET_TAG = 0x10;
        public const int EVP_CTRL_AEAD_SET_TAG = 0x11;
    }
}
