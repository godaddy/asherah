using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace GoDaddy.Asherah.SecureMemory
{
  public class TransientSecretFactory : ISecretFactory
  {
    private readonly SecureMemorySecretFactory secretFactory;

    public TransientSecretFactory(IConfiguration configuration = null)
    {
      if (configuration == null)
      {
        configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true).Build();
      }

      Debug.WriteLine("TransientSecretFactory: New");
      secretFactory = new SecureMemorySecretFactory(configuration);
    }

    public void Dispose()
    {
      Debug.WriteLine("TransientSecretFactory: Dispose");
      secretFactory.Dispose();
      GC.SuppressFinalize(this);
    }

    public Secret CreateSecret(byte[] secretData)
    {
      return secretFactory.CreateSecret(secretData);
    }

    public Secret CreateSecret(char[] secretData)
    {
      return secretFactory.CreateSecret(secretData);
    }
  }
}
