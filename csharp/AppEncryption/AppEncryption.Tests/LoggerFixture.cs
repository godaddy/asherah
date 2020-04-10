using GoDaddy.Asherah.Logging;
using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.AppEncryption.Tests
{
    public class LoggerFixture
    {
        public LoggerFixture()
        {
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
