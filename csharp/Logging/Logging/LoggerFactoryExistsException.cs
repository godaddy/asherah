using System;

namespace GoDaddy.Asherah.Logging
{
    public class LoggerFactoryExistsException : SystemException
    {
        public LoggerFactoryExistsException(string message)
            : base(message)
        {
        }
    }
}