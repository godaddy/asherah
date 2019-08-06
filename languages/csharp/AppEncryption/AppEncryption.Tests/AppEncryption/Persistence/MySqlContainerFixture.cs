using System;
using System.Threading.Tasks;
using TestContainers.Core.Builders;
using TestContainers.Core.Containers;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    public class MySqlContainerFixture : IAsyncLifetime
    {
        private const string LocalConnectionString = "Server=localhost;UID=root;pwd=Password123;SslMode=none;";
        private readonly bool useTestContainers = true;

        public MySqlContainerFixture()
        {
            string containerType = Environment.GetEnvironmentVariable("CONTAINER_TYPE");

            if (!string.IsNullOrWhiteSpace(containerType) &&
                containerType.Equals("external", StringComparison.InvariantCultureIgnoreCase))
            {
                ConnectionString = LocalConnectionString;
                useTestContainers = false;
            }
            else
            {
                Container = new DatabaseContainerBuilder<MySqlContainer>()
                    .Begin()
                    .WithImage("mysql:5.7")
                    .WithExposedPorts(3306)
                    .WithEnv(("MYSQL_ROOT_PASSWORD", "Password123"))
                    .Build();

                ConnectionString = Container.ConnectionString;
            }
        }

        public string ConnectionString { get; }

        private MySqlContainer Container { get; }

        public Task InitializeAsync()
        {
            return useTestContainers ? Container.Start() : Task.Delay(0);
        }

        public Task DisposeAsync()
        {
            return useTestContainers ? Container.Stop() : Task.Delay(0);
        }
    }
}
