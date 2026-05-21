using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.TestHelpers
{
    /// <summary>
    /// Stub implementation of ILoggerFactory for testing purposes.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class LoggerFactoryStub : ILoggerFactory
    {
        private readonly LoggerStub _loggerStub = new LoggerStub();

        /// <summary>
        /// Gets the list of log entries captured by the logger.
        /// </summary>
        public IReadOnlyList<LogEntry> LogEntries => _loggerStub.LogEntries;

        /// <inheritdoc/>
        public void AddProvider(ILoggerProvider provider)
        {
            // Stub implementation - does nothing
        }

        /// <inheritdoc/>
        public ILogger CreateLogger(string categoryName) => _loggerStub;

        /// <inheritdoc/>
        public void Dispose()
        {
            // Stub implementation - does nothing
        }
    }
}
