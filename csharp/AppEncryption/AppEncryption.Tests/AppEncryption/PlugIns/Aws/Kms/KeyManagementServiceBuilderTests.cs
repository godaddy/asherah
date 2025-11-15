using System;
using System.Diagnostics.CodeAnalysis;
using Amazon.KeyManagementService;
using Amazon.Runtime;
using GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Kms;
using GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.TestHelpers;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.PlugIns.Aws.Kms;

[ExcludeFromCodeCoverage]
public class KeyManagementServiceBuilderTests : IDisposable
{
    private readonly LoggerFactoryStub _loggerFactoryStub = new();

    [Theory]
    [InlineData(null, "arn:aws:kms:us-east-1:123456789012:key/abc", "region")]
    [InlineData("", "arn:aws:kms:us-east-1:123456789012:key/abc", "region")]
    [InlineData("us-east-1", null, "keyArn")]
    [InlineData("us-east-1", "", "keyArn")]
    public void WithRegionKeyArn_InvalidArguments_ThrowsArgumentException(string region, string keyArn, string expectedParamName)
    {
        var builder = KeyManagementService.NewBuilder();

        var ex = Assert.Throws<ArgumentException>(() => builder.WithRegionKeyArn(region, keyArn));
        Assert.Equal(expectedParamName, ex.ParamName);
    }

    [Fact]
    public void Build_WithoutLoggerFactory_ThrowsInvalidOperationException()
    {
        var builder = KeyManagementService.NewBuilder();

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal("LoggerFactory must be provided", ex.Message);
    }

    [Fact]
    public void Build_WithoutOptionsOrRegionKeyArns_ThrowsInvalidOperationException()
    {
        var builder = KeyManagementService.NewBuilder()
            .WithLoggerFactory(_loggerFactoryStub);

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal("At least one region and key ARN pair must be provided if not using WithOptions", ex.Message);
    }

    [Fact]
    public void Build_WithoutCredentialsOrClientFactory_ThrowsInvalidOperationException()
    {
        var builder = KeyManagementService.NewBuilder()
            .WithLoggerFactory(_loggerFactoryStub)
            .WithRegionKeyArn("us-east-1", "arn:aws:kms:us-east-1:123456789012:key/abc");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal("Either credentials or a KMS client factory must be provided", ex.Message);
    }

    [Fact]
    public void Build_WithAnonymousCredentials_Succeeds()
    {
        var builder = KeyManagementService.NewBuilder()
            .WithLoggerFactory(_loggerFactoryStub)
            .WithRegionKeyArn("us-east-1", "arn:aws:kms:us-east-1:123456789012:key/abc")
            .WithCredentials(new AnonymousAWSCredentials());

        var kms = builder.Build();
        Assert.NotNull(kms);
    }

    [Fact]
    public void Build_WithKmsClientFactory_Succeeds()
    {
        var clientFactory = new KeyManagementClientFactory(new AnonymousAWSCredentials());

        var builder = KeyManagementService.NewBuilder()
            .WithLoggerFactory(_loggerFactoryStub)
            .WithRegionKeyArn("us-east-1", "arn:aws:kms:us-east-1:123456789012:key/abc")
            .WithKmsClientFactory(clientFactory);

        var kms = builder.Build();
        Assert.NotNull(kms);
    }

    [Fact]
    public void Build_WithOptions_Succeeds()
    {
        var kmsOptions = new KeyManagementServiceOptions
        {
            RegionKeyArns = [new RegionKeyArn { Region = "us-east-1", KeyArn = "arn:aws:kms:us-east-1:123456789012:key/abc" }]
        };

        var builder = KeyManagementService.NewBuilder()
            .WithLoggerFactory(_loggerFactoryStub)
            .WithOptions(kmsOptions)
            .WithCredentials(new AnonymousAWSCredentials());

        var kms = builder.Build();
        Assert.NotNull(kms);
    }

    public void Dispose()
    {
        _loggerFactoryStub.Dispose();
    }
}
