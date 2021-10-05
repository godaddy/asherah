namespace GoDaddy.Asherah.SecureMemory
{
    public class SecureMemoryAllocationFailedException : SecureMemoryException
    {
        public SecureMemoryAllocationFailedException(string message)
            : base(message)
        {
        }
    }
}
