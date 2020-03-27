using GoDaddy.Asherah.Logging;
using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.SecureMemory.Tests
{
    public class LoggerFixture
    {
        public LoggerFixture()
        {
            LoggerFactory = new LoggerFactory();

            // TODO Not sure if there's a better way to handle LoggerProvider?
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
