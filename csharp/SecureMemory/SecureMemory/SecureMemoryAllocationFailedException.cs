using System;

namespace GoDaddy.Asherah.SecureMemory
{
  public class SecureMemoryAllocationFailedException : SecureMemoryException
  {
    public SecureMemoryAllocationFailedException(string message)
        : base(message)
    {
    }

    public SecureMemoryAllocationFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
  }
}
