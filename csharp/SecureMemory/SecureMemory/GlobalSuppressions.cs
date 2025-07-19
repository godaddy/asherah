// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// Suppress CA2020 for UIntPtr conversions - library is not intended for 32-bit systems
[assembly: SuppressMessage("Performance", "CA2020:Prevent behavioral change due to conversion from 'UIntPtr' to 'UInt64'", Justification = "Library is not intended to run on 32-bit systems where UIntPtr to UInt64 conversion could overflow")]

// Suppress CA1305 for locale-dependent parsing - configuration values are expected to be invariant
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "Configuration parsing uses invariant culture")]

// Suppress CA1309 for string comparisons - ordinal comparisons are used for configuration keys
[assembly: SuppressMessage("Globalization", "CA1309:Use ordinal string comparison", Justification = "Configuration key comparisons use ordinal comparison")]
