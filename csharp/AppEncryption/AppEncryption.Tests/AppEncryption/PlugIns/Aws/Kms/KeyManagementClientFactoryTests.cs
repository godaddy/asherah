using System;
using System.Diagnostics.CodeAnalysis;
using Amazon.Runtime;
using GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Kms;
using GoDaddy.Asherah.SecureMemory;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.PlugIns.Aws.Kms;

[ExcludeFromCodeCoverage]
public class KeyManagementClientFactoryTests
{
    private readonly KeyManagementClientFactory _clientFactory;

    public KeyManagementClientFactoryTests()
    {
        var credentials = new AnonymousAWSCredentials();
        _clientFactory = new KeyManagementClientFactory(credentials);
    }

    // Verify that passing invalid region throws ArgumentException
    [InlineData(null)]
    [InlineData("")]
    [Theory]
    public void TestCreateForRegion_InvalidRegion_ThrowsArgumentException(string region)
    {
        Assert.Throws<ArgumentException>((Action)CreateForRegion);
        return;

        void CreateForRegion()
        {
            _ = _clientFactory.CreateForRegion(region);
        }
    }

    // Verify that passing a non-empty region returns a non-null client
    [InlineData("us-east-1")]
    [InlineData("us-west-2")]
    [InlineData("eu-west-2")]
    [InlineData("ap-southeast-1")]
    [InlineData("invalid-region")]
    [InlineData("us-west-99")]
    [Theory]
    public void TestCreateForRegion_ValidRegion_Succeeds(string region)
    {
        var kms = _clientFactory.CreateForRegion(region);
        Assert.NotNull(kms);
    }
}
