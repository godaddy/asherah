// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "CA1848:Use LoggerMessage delegates", Justification = "Test code - logging performance not critical")]
[assembly: SuppressMessage("Design", "CA1816:Call GC.SuppressFinalize correctly", Justification = "Test classes do not need to call GC.SuppressFinalize.")]
