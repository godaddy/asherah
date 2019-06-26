using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.Logging
{
    public static class LogManager
    {
        /// <summary>
        ///  This class is modelled after an internal repo's example.
        /// </summary>
        private static volatile ILoggerFactory loggerFactory;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void SetLoggerFactory(ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException();
            }

            if (LogManager.loggerFactory != null)
            {
                throw new LoggerFactoryExistsException(
                    $"Cannot set loggerFactory to: {loggerFactory.GetType().FullName} " +
                    $"when it is already set to: {LogManager.loggerFactory.GetType().FullName}");
            }

            LogManager.loggerFactory = loggerFactory;
        }

        public static ILogger CreateLogger<T>()
            => loggerFactory?.CreateLogger<T>() ?? new LoggerFactory().CreateLogger<T>();
    }
}