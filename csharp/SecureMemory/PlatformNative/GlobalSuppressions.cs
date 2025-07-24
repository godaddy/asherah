// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// Suppress CA1707 for native interop - these names must match the native APIs
[assembly: SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Native interop requires exact name matching", Scope = "module")]

// Suppress CA1711 for native interop - these are native API names
[assembly: SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Native interop requires exact name matching", Scope = "module")]

// Suppress CA1401 for P/Invoke methods - these need to be visible for interop
[assembly: SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "P/Invoke methods must be visible for native interop", Scope = "module")]

// Suppress CA2101 for P/Invoke string arguments - these are native API calls
[assembly: SuppressMessage("Security", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "Native interop uses default marshaling", Scope = "module")]

// Suppress CA1720 for parameter names - these are native API parameter names
[assembly: SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Native interop uses native parameter names", Scope = "module")]

// Suppress CA1051 for native struct fields - these must match native layout
[assembly: SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "Native interop requires exact field layout", Scope = "module")]

// Suppress CA1806 for native function calls - return values may not be used
[assembly: SuppressMessage("Usage", "CA1806:Do not ignore method results", Justification = "Native function calls may not use return values", Scope = "module")]

// Suppress CA1069 for enum members with same values - these are native API constants
[assembly: SuppressMessage("Design", "CA1069:Enums should not have duplicate values", Justification = "Native interop requires exact constant values", Scope = "module")]

// Suppress CA2020 for UIntPtr conversions - library is not intended for 32-bit systems
[assembly: SuppressMessage("Performance", "CA2020:Prevent behavioral change due to conversion from 'UIntPtr' to 'UInt64'", Justification = "Library is not intended to run on 32-bit systems where UIntPtr to UInt64 conversion could overflow")]

// Suppress CA1305 for locale-dependent parsing - configuration values are expected to be invariant
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "Configuration parsing uses invariant culture")]

// Suppress CA1309 for string comparisons - ordinal comparisons are used for configuration keys
[assembly: SuppressMessage("Globalization", "CA1309:Use ordinal string comparison", Justification = "Configuration key comparisons use ordinal comparison")]
