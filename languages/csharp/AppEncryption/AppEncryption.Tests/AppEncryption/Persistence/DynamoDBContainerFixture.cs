using System.Threading.Tasks;
using TestContainers.Core.Builders;
using TestContainers.Core.Containers;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    public class DynamoDBContainerFixture : IAsyncLifetime
    {
        public DynamoDBContainerFixture() =>
            DynamoDbContainer = new GenericContainerBuilder<Container>()
                .Begin()
                .WithImage("amazon/dynamodb-local:latest")
                .WithExposedPorts(8000)
                .Build();

        public string ServiceURL =>
            $"http://{DynamoDbContainer.GetDockerHostIpAddress()}:{DynamoDbContainer.ExposedPorts[0]}";

        private Container DynamoDbContainer { get; }

        public Task InitializeAsync() => DynamoDbContainer.Start();

        public Task DisposeAsync() => DynamoDbContainer.Stop();
    }
}
