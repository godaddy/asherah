using System;
using System.Threading.Tasks;
using TestContainers.Core.Builders;
using TestContainers.Core.Containers;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    public class MySqlContainerFixture : IAsyncLifetime
    {
        private const string LocalHost = "server=localhost;";
        private const string LocalConnectionString = "uid=root;pwd=Password123;sslmode=none;";
        private readonly bool disableTestContainers;

        public MySqlContainerFixture()
        {
            disableTestContainers = Convert.ToBoolean(Environment.GetEnvironmentVariable("DISABLE_TESTCONTAINERS"));

            if (disableTestContainers)
            {
                string hostname = Environment.GetEnvironmentVariable("MYSQL_HOSTNAME");
                if (hostname == null)
                {
                    ConnectionString = LocalHost + LocalConnectionString;
                }
                else
                {
                    ConnectionString = $"server={hostname};{LocalConnectionString}";
                }
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
            return disableTestContainers ? Task.Delay(0) : Container.Start();
        }

        public Task DisposeAsync()
        {
            return disableTestContainers ? Task.Delay(0) : Container.Stop();
        }
    }
}
