using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests
{
    /// <summary>
    /// Provides a shared LoggerFactory for IntegrationTests to use.
    /// This eliminates the need to duplicate logger setup in each test class.
    /// </summary>
    public static class TestLoggerFactory
    {
        private static readonly ILoggerFactory _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });

        /// <summary>
        /// Gets the shared LoggerFactory instance.
        /// </summary>
        public static ILoggerFactory LoggerFactory => _loggerFactory;

        /// <summary>
        /// Creates a logger for the specified type.
        /// </summary>
        /// <typeparam name="T">The type to create a logger for.</typeparam>
        /// <returns>An ILogger instance for the specified type.</returns>
        public static ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();
    }
}
