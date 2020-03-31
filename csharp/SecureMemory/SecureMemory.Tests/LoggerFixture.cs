using GoDaddy.Asherah.Logging;
using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.SecureMemory.Tests
{
    public class LoggerFixture
    {
        public LoggerFixture()
        {
            LoggerFactory = new LoggerFactory();

            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder.AddFilter((category, level) => level >= LogLevel.Information)
                    .AddConsole();
            });
            LogManager.SetLoggerFactory(LoggerFactory);
        }

        public ILoggerFactory LoggerFactory { get; private set;  }
    }
}
