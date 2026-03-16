using System;
using System.Diagnostics.CodeAnalysis;
using Amazon.DynamoDBv2;
using GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Metastore;
using Moq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.PlugIns.Aws.Metastore;

[ExcludeFromCodeCoverage]
public class DynamoDbMetastoreBuilderTests
{
    [Fact]
    public void Build_WithoutDynamoDbClient_ThrowsInvalidOperationException()
    {
        var builder = DynamoDbMetastore.NewBuilder();

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal("DynamoDB client must be set using WithDynamoDbClient()", ex.Message);
    }

    [Fact]
    public void Build_WithDynamoDbClient_Succeeds()
    {
        var mockClient = new Mock<IAmazonDynamoDB>();
        var builder = DynamoDbMetastore.NewBuilder()
            .WithDynamoDbClient(mockClient.Object);

        var metastore = builder.Build();

        Assert.NotNull(metastore);
    }

    [Fact]
    public void Build_WithDynamoDbClientAndOptions_Succeeds()
    {
        var mockClient = new Mock<IAmazonDynamoDB>();
        var options = new DynamoDbMetastoreOptions
        {
            KeyRecordTableName = "CustomTable",
            KeySuffix = "us-west-2"
        };

        var builder = DynamoDbMetastore.NewBuilder()
            .WithDynamoDbClient(mockClient.Object)
            .WithOptions(options);

        var metastore = builder.Build();

        Assert.NotNull(metastore);
    }

    [Fact]
    public void Build_WithOptionsHavingEmptyTableName_ThrowsArgumentException()
    {
        var mockClient = new Mock<IAmazonDynamoDB>();
        var options = new DynamoDbMetastoreOptions
        {
            KeyRecordTableName = string.Empty
        };

        var builder = DynamoDbMetastore.NewBuilder()
            .WithDynamoDbClient(mockClient.Object)
            .WithOptions(options);

        var ex = Assert.Throws<ArgumentException>(() => builder.Build());
        Assert.Contains("KeyRecordTableName", ex.Message);
    }

    [Fact]
    public void Build_WithOptionsHavingNullTableName_ThrowsArgumentException()
    {
        var mockClient = new Mock<IAmazonDynamoDB>();
        var options = new DynamoDbMetastoreOptions
        {
            KeyRecordTableName = null
        };

        var builder = DynamoDbMetastore.NewBuilder()
            .WithDynamoDbClient(mockClient.Object)
            .WithOptions(options);

        var ex = Assert.Throws<ArgumentException>(() => builder.Build());
        Assert.Contains("KeyRecordTableName", ex.Message);
    }

    [Fact]
    public void Build_WithoutOptions_UsesDefaultOptionsAndReturnsClientRegion()
    {
        var mockConfig = new AmazonDynamoDBConfig
        {
            RegionEndpoint = Amazon.RegionEndpoint.APSouth2
        };
        var mockClient = new Mock<IAmazonDynamoDB>();
        mockClient.Setup(x => x.Config).Returns(mockConfig);

        var builder = DynamoDbMetastore.NewBuilder()
            .WithDynamoDbClient(mockClient.Object);

        var metastore = builder.Build();

        Assert.NotNull(metastore);
        // Default KeySuffix is null, so GetKeySuffix returns the client's region from config
        Assert.Equal("ap-south-2", metastore.GetKeySuffix());
    }

    [Fact]
    public void Build_WithKeySuffixDisabled_ReturnsEmptyKeySuffix()
    {
        var mockClient = new Mock<IAmazonDynamoDB>();
        var options = new DynamoDbMetastoreOptions
        {
            KeySuffix = string.Empty
        };

        var builder = DynamoDbMetastore.NewBuilder()
            .WithDynamoDbClient(mockClient.Object)
            .WithOptions(options);

        var metastore = builder.Build();

        Assert.NotNull(metastore);
        Assert.Equal(string.Empty, metastore.GetKeySuffix());
    }

    [Fact]
    public void Build_WithCustomKeySuffix_ReturnsCustomKeySuffix()
    {
        var mockClient = new Mock<IAmazonDynamoDB>();
        var options = new DynamoDbMetastoreOptions
        {
            KeySuffix = "my-custom-suffix"
        };

        var builder = DynamoDbMetastore.NewBuilder()
            .WithDynamoDbClient(mockClient.Object)
            .WithOptions(options);

        var metastore = builder.Build();

        Assert.NotNull(metastore);
        Assert.Equal("my-custom-suffix", metastore.GetKeySuffix());
    }

    [Fact]
    public void Build_WithCustomTableNameAndDefaultKeySuffix_UsesClientRegion()
    {
        var mockConfig = new AmazonDynamoDBConfig
        {
            RegionEndpoint = Amazon.RegionEndpoint.AFSouth1
        };
        var mockClient = new Mock<IAmazonDynamoDB>();
        mockClient.Setup(x => x.Config).Returns(mockConfig);

        var options = new DynamoDbMetastoreOptions
        {
            KeyRecordTableName = "MyCustomTable"
            // KeySuffix is null (default) - should use client's region
        };

        var builder = DynamoDbMetastore.NewBuilder()
            .WithDynamoDbClient(mockClient.Object)
            .WithOptions(options);

        var metastore = builder.Build();

        Assert.NotNull(metastore);
        // KeySuffix is null, so GetKeySuffix should return the client's region
        Assert.Equal("af-south-1", metastore.GetKeySuffix());
    }

    [Fact]
    public void Build_WithCustomTableNameAndDefaultKeySuffix_SupportsNoRegion()
    {
        var mockConfig = new AmazonDynamoDBConfig
        {
            // No RegionEndpoint set - simulates using ServiceURL for local DynamoDB
            ServiceURL = "http://localhost:8000"
        };
        var mockClient = new Mock<IAmazonDynamoDB>();
        mockClient.Setup(x => x.Config).Returns(mockConfig);

        var options = new DynamoDbMetastoreOptions
        {
            KeyRecordTableName = "MyCustomTable"
            // KeySuffix is null (default) - should use client's region
        };

        var builder = DynamoDbMetastore.NewBuilder()
            .WithDynamoDbClient(mockClient.Object)
            .WithOptions(options);

        var metastore = builder.Build();

        Assert.NotNull(metastore);
        // KeySuffix is null and no RegionEndpoint, so GetKeySuffix should return null
        Assert.Null(metastore.GetKeySuffix());
    }
}
