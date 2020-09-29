using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TestContainers.Core.Builders;
using TestContainers.Core.Containers;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    public class DynamoDBContainerFixture : IAsyncLifetime
    {
        private const string LocalServiceUrl = "http://localhost:8000";
        private readonly bool disableTestContainers;

        public DynamoDBContainerFixture()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                disableTestContainers = Convert.ToBoolean(Environment.GetEnvironmentVariable("DISABLE_TESTCONTAINERS"));

                if (disableTestContainers)
                {
                    ServiceUrl = LocalServiceUrl;
                }
                else
                {
                    DynamoDbContainer = new GenericContainerBuilder<Container>()
                        .Begin()
                        .WithImage("amazon/dynamodb-local:latest")
                        .WithExposedPorts(8000)
                        .Build();

                    ServiceUrl =
                        $"http://{DynamoDbContainer.GetDockerHostIpAddress()}:{DynamoDbContainer.ExposedPorts[0]}";
                }
            }
        }

        public string ServiceUrl { get; }

        private Container DynamoDbContainer { get; }

        public Task InitializeAsync()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return disableTestContainers ? Task.Delay(0) : DynamoDbContainer.Start();
            }

            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return disableTestContainers ? Task.Delay(0) : DynamoDbContainer.Stop();
            }

            return Task.CompletedTask;
        }
    }
}
