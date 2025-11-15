using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.TestHelpers
{
    /// <summary>
    /// Record representing a log entry for testing purposes.
    /// </summary>
    /// <param name="LogLevel">The log level of the entry.</param>
    /// <param name="EventId">The event ID of the entry.</param>
    /// <param name="Exception">The exception associated with the entry, if any.</param>
    /// <param name="Message">The log message.</param>
    [ExcludeFromCodeCoverage]
    public record LogEntry(LogLevel LogLevel, EventId EventId, Exception Exception, string Message);
}
