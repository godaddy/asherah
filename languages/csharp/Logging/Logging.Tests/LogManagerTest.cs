using System;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GoDaddy.Asherah.Logging.Tests
{
    public class LogManagerTest
    {
        private ILoggerFactory loggerFactory;

        [Fact]
        public void TestWithNullLoggerFactory()
        {
            Assert.Throws<ArgumentNullException>(() => LogManager.SetLoggerFactory(null));
        }

        [Fact]
        public void TestCreateLogger()
        {
            // CreateLogger with no LoggerFactory creates a new (not null) logger
            var iLogger = LogManager.CreateLogger<ILogger>();
            Assert.NotNull(iLogger);

            // CreateLogger called after SetLoggerFactory toggles the boolean in the TestLoggerProvider class
            loggerFactory = new LoggerFactory();
            var testLogProvider = new TestLoggerProvider();
            loggerFactory.AddProvider(testLogProvider);
            LogManager.SetLoggerFactory(loggerFactory);
            LogManager.CreateLogger<ILogger>();
            Assert.True(testLogProvider.GetLoggerUsed());

            // Trying to set the LoggerFactory for a second time throws an exception
            Assert.Throws<LoggerFactoryExistsException>(() => LogManager.SetLoggerFactory(loggerFactory));
        }
    }
}
