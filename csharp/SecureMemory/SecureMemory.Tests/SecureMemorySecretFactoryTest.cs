using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.ProtectedMemoryImpl.MacOS;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.Linux;
using GoDaddy.Asherah.SecureMemory.SecureMemoryImpl.MacOS;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoDaddy.Asherah.SecureMemory.Tests
{
  [Collection("Logger Fixture collection")]
  public class SecureMemorySecretFactoryTest
  {
    private static readonly byte[] TestBytes = new byte[] { 0, 1 };
    private static readonly char[] TestChars = new[] { 'a', 'b' };
    private readonly IConfiguration configuration;

    public SecureMemorySecretFactoryTest()
    {
      Trace.Listeners.Clear();
      var consoleListener = new ConsoleTraceListener();
      Trace.Listeners.Add(consoleListener);

      var configDictionary = new Dictionary<string, string>();
      configDictionary["debugSecrets"] = "true";

      configuration = new ConfigurationBuilder()
          .AddInMemoryCollection(configDictionary)
          .Build();
    }

    // TODO Mocking static methods is not yet possible in Moq framework.
    // If it gets possible then we can add test these flows
    [Fact]
    private void TestSecureMemorySecretFactoryWithMac()
    {
    }

    [Fact]
    private void TestSecureMemorySecretFactoryWithLinux()
    {
    }

    [Fact]
    private void TestSecureMemorySecretFactoryWithWindowsShouldFail()
    {
    }

    [Fact]
    private void TestMmapConfiguration()
    {
      var testConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                {"secureHeapEngine", "mmap"}
            }).Build();

      Debug.WriteLine("SecureMemorySecretFactoryTest.TestMmapConfiguration");
      using (var factory = new SecureMemorySecretFactory(testConfiguration))
      {
      }
    }

    [Fact]
    private void TestInvalidConfiguration()
    {
      var testConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                {"secureHeapEngine", "magic-heap-engine2"}
            }).Build();

      Debug.WriteLine("SecureMemorySecretFactoryTest.TestMmapConfiguration");
      Assert.Throws<PlatformNotSupportedException>(() =>
      {
        using (var factory = new SecureMemorySecretFactory(testConfiguration))
        {
        }
      });
    }

    [Fact]
    private void TestCreateSecretByteArray()
    {
      Debug.WriteLine("SecureMemorySecretFactoryTest.TestCreateSecretByteArray");
      using var factory = new SecureMemorySecretFactory(configuration);
      using var secret = factory.CreateSecret(TestBytes);
      Assert.Equal(typeof(SecureMemorySecret), secret.GetType());
    }

    [Fact]
    private void TestCreateSecretCharArray()
    {
      Debug.WriteLine("SecureMemorySecretFactoryTest.TestCreateSecretCharArray");
      using var factory = new SecureMemorySecretFactory(configuration);
      using var secret = factory.CreateSecret(TestChars);
      Assert.Equal(typeof(SecureMemorySecret), secret.GetType());
    }

    [Fact]
    private void TestDoubleDispose()
    {
      var factory = new SecureMemorySecretFactory(configuration);
      factory.Dispose();
      Assert.Throws<SecureMemoryException>(() =>
      {
        factory.Dispose();
      });
    }

    [Fact]
    private void TestMlockConfigurationSettingForMac()
    {
      Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Test only runs on macOS");

      var testConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                {"mlock", "disabled"}
            }).Build();

      var allocator = SecureMemorySecretFactory.ConfigureForMacOS64(testConfiguration);

      Assert.IsType<MacOSSecureMemoryAllocatorLP64>(allocator);
      Assert.IsNotType<MacOSProtectedMemoryAllocatorLP64>(allocator);
    }

    [Fact]
    private void TestMlockConfigurationSettingForLinux()
    {
      Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Linux), "Test only runs on Linux");

      var testConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                {"mlock", "disabled"}
            }).Build();

      var allocator = SecureMemorySecretFactory.ConfigureForLinux64(testConfiguration);

      Assert.IsType<LinuxSecureMemoryAllocatorLP64>(allocator);
      Assert.IsNotType<LinuxProtectedMemoryAllocatorLP64>(allocator);
    }

    [Fact]
    private void TestMlockConfigurationSettingForMacWithInvalidValueThrowsException()
    {
      Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Test only runs on macOS");

      var testConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                {"mlock", "false"}
            }).Build();

      Assert.Throws<ConfigurationErrorsException>(() => SecureMemorySecretFactory.ConfigureForMacOS64(testConfiguration));
    }

    [Fact]
    private void TestMlockConfigurationSettingForLinuxWithInvalidValueThrowsException()
    {
      Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Linux), "Test only runs on Linux");

      var testConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                {"mlock", "no"}
            }).Build();

      Assert.Throws<ConfigurationErrorsException>(() => SecureMemorySecretFactory.ConfigureForLinux64(testConfiguration));
    }

    [Fact]
    private void TestDefaultMlockConfigurationForLinux()
    {
      Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Linux), "Test only runs on Linux");

      var allocator = SecureMemorySecretFactory.ConfigureForLinux64(configuration);

      Assert.IsType<LinuxProtectedMemoryAllocatorLP64>(allocator);
      Assert.IsNotType<LinuxSecureMemoryAllocatorLP64>(allocator);
    }

    [Fact]
    private void TestDefaultMlockConfigurationForMac()
    {
      Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.OSX), "Test only runs on macOS");

      var allocator = SecureMemorySecretFactory.ConfigureForMacOS64(configuration);

      Assert.IsType<MacOSProtectedMemoryAllocatorLP64>(allocator);
      Assert.IsNotType<MacOSSecureMemoryAllocatorLP64>(allocator);
    }
  }
}
