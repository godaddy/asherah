namespace GoDaddy.Asherah.SecureMemory
{
    public interface ISecretFactory
    {
        Secret CreateSecret(byte[] secretData);

        Secret CreateSecret(char[] secretData);
    }
}
