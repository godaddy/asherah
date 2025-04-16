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
                Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "dummy");
                Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "secret");

                dynamoDbContainer = new DynamoDbBuilder()
                    .Build();
            }
        }

        public Task InitializeAsync() => dynamoDbContainer.StartAsync();

        public Task DisposeAsync() => dynamoDbContainer.StopAsync();

        public string GetServiceUrl() => dynamoDbContainer?.GetConnectionString() ?? localServiceUrl;
    }
}
