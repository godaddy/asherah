using GoDaddy.Asherah.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace GoDaddy.Asherah.AppEncryption.Tests
{
    public class LoggerFixture
    {
        public LoggerFixture()
        {
            LoggerFactory = new LoggerFactory();

            // TODO Not sure if there's a better way to handle LoggerProvider?
            #pragma warning disable 618
            LoggerFactory.AddProvider(new ConsoleLoggerProvider((category, level) => level >= LogLevel.Information, true));
            #pragma warning restore 618
            LogManager.SetLoggerFactory(LoggerFactory);
        }

        public ILoggerFactory LoggerFactory { get; private set;  }
    }
}
