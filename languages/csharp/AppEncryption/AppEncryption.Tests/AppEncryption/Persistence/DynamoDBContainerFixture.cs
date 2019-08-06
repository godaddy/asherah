using System;
using System.Threading.Tasks;
using TestContainers.Core.Builders;
using TestContainers.Core.Containers;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    public class DynamoDBContainerFixture : IAsyncLifetime
    {
        private const string LocalServiceUrl = "http://localhost:8000";
        private readonly bool useTestContainers = true;

        public DynamoDBContainerFixture()
        {
            string containerType = Environment.GetEnvironmentVariable("CONTAINER_TYPE");

            if (!string.IsNullOrWhiteSpace(containerType) &&
                containerType.Equals("EXTERNAL", StringComparison.InvariantCultureIgnoreCase))
            {
                ServiceUrl = LocalServiceUrl;
                useTestContainers = false;
            }
            else
            {
                DynamoDbContainer = new GenericContainerBuilder<Container>()
                    .Begin()
                    .WithImage("amazon/dynamodb-local:latest")
                    .WithExposedPorts(8000)
                    .Build();

                ServiceUrl = $"http://{DynamoDbContainer.GetDockerHostIpAddress()}:{DynamoDbContainer.ExposedPorts[0]}";
            }
        }

        public string ServiceUrl { get; }

        private Container DynamoDbContainer { get; }

        public Task InitializeAsync()
        {
            return useTestContainers ? DynamoDbContainer.Start() : Task.Delay(0);
        }

        public Task DisposeAsync()
        {
            return useTestContainers ? DynamoDbContainer.Stop() : Task.Delay(0);
        }
    }
}
