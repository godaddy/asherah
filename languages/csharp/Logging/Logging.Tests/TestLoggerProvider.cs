using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.Logging.Tests
{
    /// <summary>
    /// Created from https://stackoverflow.com/questions/53494199/custom-implementation-of-ilogger
    /// </summary>
    public class TestLoggerProvider : ILoggerProvider
    {
        private bool loggerUsed;

        public bool GetLoggerUsed()
        {
            return loggerUsed;
        }

        public ILogger CreateLogger(string categoryName)
        {
            loggerUsed = true;
            return new TestLogger(categoryName);
        }

        public void Dispose()
        {
        }
    }
}