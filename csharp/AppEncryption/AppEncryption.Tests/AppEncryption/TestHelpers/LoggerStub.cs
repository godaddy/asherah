using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.TestHelpers
{
    /// <summary>
    /// Stub implementation of ILogger for testing purposes.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class LoggerStub : ILogger
    {
        private readonly List<LogEntry> _logEntries = new List<LogEntry>();

        /// <summary>
        /// Gets the list of log entries captured by this logger.
        /// </summary>
        public IReadOnlyList<LogEntry> LogEntries => _logEntries.AsReadOnly();

        /// <inheritdoc/>
        public IDisposable BeginScope<TState>(TState state) => null;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter?.Invoke(state, exception) ?? state?.ToString() ?? string.Empty;
            _logEntries.Add(new LogEntry(logLevel, eventId, exception, message));
        }
    }
}
