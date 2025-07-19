using System;
using System.Globalization;
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
            var disableTestContainers = Convert.ToBoolean(Environment.GetEnvironmentVariable("DISABLE_TESTCONTAINERS"), CultureInfo.InvariantCulture);

            if (disableTestContainers)
            {
                string hostname = Environment.GetEnvironmentVariable("MYSQL_HOSTNAME") ?? "localhost";
                localConnectionString = $"server={hostname};uid=root;pwd=Password123;";
            }
            else
            {
                container = new MySqlBuilder()
                    .WithUsername("root")
                    .Build();
            }
        }

        public ValueTask InitializeAsync() => new ValueTask(container?.StartAsync() ?? Task.CompletedTask);

        public ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            return new ValueTask(container?.StopAsync() ?? Task.CompletedTask);
        }

        public string GetConnectionString() => container?.GetConnectionString() ?? localConnectionString;
    }
}
