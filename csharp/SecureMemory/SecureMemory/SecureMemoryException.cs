using System;

namespace GoDaddy.Asherah.SecureMemory
{
  public class SecureMemoryException : SystemException
  {
    public SecureMemoryException(string message)
        : base(message)
    {
    }

    public SecureMemoryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
  }
}
