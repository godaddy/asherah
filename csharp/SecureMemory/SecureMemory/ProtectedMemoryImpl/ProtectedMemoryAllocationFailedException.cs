namespace GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl
{
    public class ProtectedMemoryAllocationFailedException : ProtectedMemoryException
    {
        public ProtectedMemoryAllocationFailedException(string message)
            : base(message)
        {
        }
    }
}
