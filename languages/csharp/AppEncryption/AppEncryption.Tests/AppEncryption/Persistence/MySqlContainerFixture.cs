using System.Threading.Tasks;
using TestContainers.Core.Builders;
using TestContainers.Core.Containers;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    public class MySqlContainerFixture : IAsyncLifetime
    {
        public MySqlContainerFixture() =>
            Container = new DatabaseContainerBuilder<MySqlContainer>()
                .Begin()
                .WithImage("mysql:5.7")
                .WithExposedPorts(3306)
                .WithEnv(("MYSQL_ROOT_PASSWORD", "Password123"))
                .Build();

        public string ConnectionString => Container.ConnectionString;

        private MySqlContainer Container { get; }

        public Task InitializeAsync() => Container.Start();

        public Task DisposeAsync() => Container.Stop();
    }
}
