using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using Testcontainers.DynamoDb;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.Fixtures;

[ExcludeFromCodeCoverage]
public class DynamoDbContainerFixture : IAsyncLifetime
{
    private readonly string _localServiceUrl;
    private readonly DynamoDbContainer _dynamoDbContainer;

    public DynamoDbContainerFixture()
    {
        var disableTestContainers = Convert.ToBoolean(Environment.GetEnvironmentVariable("DISABLE_TESTCONTAINERS"), CultureInfo.InvariantCulture);

        if (disableTestContainers)
        {
            var hostname = Environment.GetEnvironmentVariable("DYNAMODB_HOSTNAME") ?? "localhost";
            _localServiceUrl = $"http://{hostname}:8000";
        }
        else
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "dummykey");
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "dummy_secret");

            _dynamoDbContainer = new DynamoDbBuilder()
                .WithImage("amazon/dynamodb-local:2.6.0")
                .Build();
        }
    }

    public Task InitializeAsync() => _dynamoDbContainer?.StartAsync() ?? Task.CompletedTask;

    public Task DisposeAsync() => _dynamoDbContainer?.StopAsync() ?? Task.CompletedTask;

    public string GetServiceUrl() => _dynamoDbContainer?.GetConnectionString() ?? _localServiceUrl;
}
