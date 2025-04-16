using System;
using System.Threading.Tasks;
using Testcontainers.MySql;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Persistence
{
    public class MySqlContainerFixture : IAsyncLifetime
    {
        private readonly string localConnectionString;
        private readonly MySqlContainer container;

        public MySqlContainerFixture()
        {
            var disableTestContainers = Convert.ToBoolean(Environment.GetEnvironmentVariable("DISABLE_TESTCONTAINERS"));

            if (disableTestContainers)
            {
                string hostname = Environment.GetEnvironmentVariable("MYSQL_HOSTNAME") ?? "localhost";
                localConnectionString = $"server={hostname};uid=root;pwd=Password123;sslmode=none";
            }
            else
            {
                container = new MySqlBuilder()
                    .WithUsername("root")
                    .Build();
            }
        }

        public Task InitializeAsync() => container.StartAsync();

        public Task DisposeAsync() => container.StopAsync();

        public string GetConnectionString() => container?.GetConnectionString() ?? localConnectionString;
    }
}
