using System;
using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.Logging.Tests
{
    /// <summary>
    /// Created from https://stackoverflow.com/questions/53494199/custom-implementation-of-ilogger
    /// </summary>
    public class TestLogger : ILogger
    {
        private readonly string categoryName;

        public TestLogger(string categoryName)
        {
            this.categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return new NoopDisposable();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            string message = string.Empty;

            if (formatter != null)
            {
                message += formatter(state, exception);
            }

// Log to console
            Console.WriteLine($"{logLevel.ToString()} - {eventId.Id} - {categoryName} - {message}");
        }

        private class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}