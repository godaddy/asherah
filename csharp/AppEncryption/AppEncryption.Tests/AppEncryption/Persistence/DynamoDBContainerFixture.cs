using System;
using System.Threading.Tasks;
using Testcontainers.DynamoDb;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    public class DynamoDBContainerFixture : IAsyncLifetime
    {
        private readonly string localServiceUrl;
        private readonly DynamoDbContainer dynamoDbContainer;

        public DynamoDBContainerFixture()
        {
            var disableTestContainers = Convert.ToBoolean(Environment.GetEnvironmentVariable("DISABLE_TESTCONTAINERS"));

            if (disableTestContainers)
            {
                string hostname = Environment.GetEnvironmentVariable("DYNAMODB_HOSTNAME") ?? "localhost";
                localServiceUrl = $"http://{hostname}:8000";
            }
            else
            {
                Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "dummykey");
                Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "dummy_secret");

                dynamoDbContainer = new DynamoDbBuilder()
                    .WithImage("amazon/dynamodb-local:2.6.0")
                    .Build();
            }
        }

        public Task InitializeAsync() => dynamoDbContainer?.StartAsync() ?? Task.CompletedTask;

        public Task DisposeAsync() => dynamoDbContainer?.StopAsync() ?? Task.CompletedTask;

        public string GetServiceUrl() => dynamoDbContainer?.GetConnectionString() ?? localServiceUrl;
    }
}
