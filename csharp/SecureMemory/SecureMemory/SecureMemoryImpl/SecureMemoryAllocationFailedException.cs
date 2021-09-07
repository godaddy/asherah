namespace GoDaddy.Asherah.SecureMemory.SecureMemoryImpl
{
    public class SecureMemoryAllocationFailedException : SecureMemoryException
    {
        public SecureMemoryAllocationFailedException(string message)
            : base(message)
        {
        }
    }
}
