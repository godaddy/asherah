using System.Diagnostics.CodeAnalysis;

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

[assembly: SuppressMessage("Performance", "CA1816:Call GC.SuppressFinalize correctly", Justification = "Test classes don't need finalizers or SuppressFinalize calls")]
