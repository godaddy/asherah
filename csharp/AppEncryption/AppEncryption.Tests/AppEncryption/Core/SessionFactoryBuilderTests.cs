using System;
using GoDaddy.Asherah.AppEncryption.Core;
using GoDaddy.Asherah.AppEncryption.Kms;
using GoDaddy.Asherah.AppEncryption.Metastore;
using GoDaddy.Asherah.AppEncryption.PlugIns.Testing.Metastore;
using GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.TestHelpers;
using GoDaddy.Asherah.AppEncryption.PlugIns.Testing.Kms;
using GoDaddy.Asherah.Crypto;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Core
{
    public class SessionFactoryBuilderTests
    {
        private const string TestProductId = "test_product_id";
        private const string TestServiceId = "test_service_id";

        private static ISessionFactoryBuilder NewBuilder() =>
            GoDaddy.Asherah.AppEncryption.Core.SessionFactory.NewBuilder(TestProductId, TestServiceId);

        private static InMemoryKeyMetastore CreateMetastore() => new InMemoryKeyMetastore();

        private static BasicExpiringCryptoPolicy CreateCryptoPolicy() =>
            BasicExpiringCryptoPolicy.NewBuilder()
                .WithKeyExpirationDays(1)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(false)
                .Build();

        private static StaticKeyManagementService CreateKeyManagementService() => new StaticKeyManagementService();

        private static ILogger CreateLogger() => new LoggerFactoryStub().CreateLogger(nameof(SessionFactoryBuilderTests));

        [Fact]
        public void NewBuilder_WithValidIds_ReturnsBuilder()
        {
            var builder = GoDaddy.Asherah.AppEncryption.Core.SessionFactory.NewBuilder(TestProductId, TestServiceId);

            Assert.NotNull(builder);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void NewBuilder_WithInvalidProductId_ThrowsArgumentException(string productId)
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                GoDaddy.Asherah.AppEncryption.Core.SessionFactory.NewBuilder(productId, TestServiceId));
            Assert.Equal("productId", ex.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void NewBuilder_WithInvalidServiceId_ThrowsArgumentException(string serviceId)
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                GoDaddy.Asherah.AppEncryption.Core.SessionFactory.NewBuilder(TestProductId, serviceId));
            Assert.Equal("serviceId", ex.ParamName);
        }

        [Fact]
        public void Build_WithAllDependencies_ReturnsSessionFactory()
        {
            using var metastore = CreateMetastore();
            using var keyManagementService = CreateKeyManagementService();
            using var factory = NewBuilder()
                .WithKeyMetastore(metastore)
                .WithCryptoPolicy(CreateCryptoPolicy())
                .WithKeyManagementService(keyManagementService)
                .WithLogger(CreateLogger())
                .Build();

            Assert.NotNull(factory);
        }

        [Fact]
        public void Build_WithoutKeyMetastore_ThrowsInvalidOperationException()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                NewBuilder()
                    .WithCryptoPolicy(CreateCryptoPolicy())
                    .WithKeyManagementService(CreateKeyManagementService())
                    .WithLogger(CreateLogger())
                    .Build());
            Assert.Contains("Key metastore", ex.Message);
        }

        [Fact]
        public void Build_WithoutCryptoPolicy_ThrowsInvalidOperationException()
        {
            using var metastore = new InMemoryKeyMetastore();
            var ex = Assert.Throws<InvalidOperationException>(() =>
                NewBuilder()
                    .WithKeyMetastore(metastore)
                    .WithKeyManagementService(CreateKeyManagementService())
                    .WithLogger(CreateLogger())
                    .Build());
            Assert.Contains("Crypto policy", ex.Message);
        }

        [Fact]
        public void Build_WithoutKeyManagementService_ThrowsInvalidOperationException()
        {
            using var metastore = new InMemoryKeyMetastore();
            var ex = Assert.Throws<InvalidOperationException>(() =>
                NewBuilder()
                    .WithKeyMetastore(metastore)
                    .WithCryptoPolicy(CreateCryptoPolicy())
                    .WithLogger(CreateLogger())
                    .Build());
            Assert.Contains("Key management service", ex.Message);
        }

        [Fact]
        public void Build_WithoutLogger_ThrowsInvalidOperationException()
        {
            using var metastore = new InMemoryKeyMetastore();
            var ex = Assert.Throws<InvalidOperationException>(() =>
                NewBuilder()
                    .WithKeyMetastore(metastore)
                    .WithCryptoPolicy(CreateCryptoPolicy())
                    .WithKeyManagementService(CreateKeyManagementService())
                    .Build());
            Assert.Contains("Logger", ex.Message);
        }

        [Fact]
        public void WithKeyMetastore_ReturnsSameBuilderForChaining()
        {
            using var metastore = new InMemoryKeyMetastore();
            var builder = NewBuilder().WithKeyMetastore(metastore);

            Assert.NotNull(builder);
            Assert.Same(builder, builder.WithCryptoPolicy(CreateCryptoPolicy()));
        }

        [Fact]
        public void WithCryptoPolicy_ReturnsSameBuilderForChaining()
        {
            var builder = NewBuilder().WithCryptoPolicy(CreateCryptoPolicy());

            Assert.NotNull(builder);
            Assert.Same(builder, builder.WithKeyManagementService(CreateKeyManagementService()));
        }

        [Fact]
        public void WithKeyManagementService_ReturnsSameBuilderForChaining()
        {
            var builder = NewBuilder().WithKeyManagementService(CreateKeyManagementService());

            Assert.NotNull(builder);
            Assert.Same(builder, builder.WithLogger(CreateLogger()));
        }

        [Fact]
        public void WithLogger_ReturnsSameBuilderForChaining()
        {
            var builder = NewBuilder().WithLogger(CreateLogger());

            Assert.NotNull(builder);
            Assert.Same(builder, builder.WithKeyMetastore(CreateMetastore()));
        }
    }
}
