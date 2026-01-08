using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Kms;
using GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.TestHelpers;
using Microsoft.Extensions.Logging;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.PlugIns.Aws.Kms
{
    /// <summary>
    /// Builder for creating KeyManagementService instances in tests with clear, explicit configuration.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class KeyManagementServiceTestBuilder
    {
        private readonly List<RegionKeyArn> _regionKeyArns = new();
        private readonly HashSet<string> _brokenRegions = new();
        private ILoggerFactory _loggerFactory = null;

        private KeyManagementServiceTestBuilder()
        {
        }

        /// <summary>
        /// Creates a new builder instance.
        /// </summary>
        public static KeyManagementServiceTestBuilder Create()
        {
            return new KeyManagementServiceTestBuilder();
        }

        /// <summary>
        /// Adds a region with its ARN. The ARN will be automatically generated if not provided.
        /// </summary>
        public KeyManagementServiceTestBuilder WithRegion(string region, string arn = null)
        {
            _regionKeyArns.Add(new RegionKeyArn
            {
                Region = region,
                KeyArn = arn ?? $"arn-{region}"
            });
            return this;
        }

        /// <summary>
        /// Adds multiple regions with their ARNs. ARNs will be automatically generated if not provided.
        /// </summary>
        public KeyManagementServiceTestBuilder WithRegions(params (string Region, string Arn)[] regions)
        {
            foreach (var (region, arn) in regions)
            {
                WithRegion(region, arn);
            }
            return this;
        }

        /// <summary>
        /// Marks a region as broken (will throw errors). The region must already be added.
        /// </summary>
        public KeyManagementServiceTestBuilder WithBrokenRegion(string region)
        {
            _brokenRegions.Add(region);
            return this;
        }

        /// <summary>
        /// Marks multiple regions as broken. The regions must already be added.
        /// </summary>
        public KeyManagementServiceTestBuilder WithBrokenRegions(params string[] regions)
        {
            foreach (var region in regions)
            {
                _brokenRegions.Add(region);
            }
            return this;
        }

        /// <summary>
        /// Sets a custom logger factory. If not specified, a LoggerFactoryStub will be used.
        /// </summary>
        public KeyManagementServiceTestBuilder WithLoggerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            return this;
        }

        /// <summary>
        /// Builds the KeyManagementService with the configured options.
        /// </summary>
        public KeyManagementService Build()
        {
            if (_regionKeyArns.Count == 0)
            {
                throw new InvalidOperationException("At least one region must be specified.");
            }

            // Mark broken regions with "ERROR" ARN
            var options = new KeyManagementServiceOptions
            {
                RegionKeyArns = _regionKeyArns.Select(rka =>
                    new RegionKeyArn
                    {
                        Region = rka.Region,
                        KeyArn = _brokenRegions.Contains(rka.Region) ? "ERROR" : rka.KeyArn
                    }).ToList()
            };

            var loggerFactory = _loggerFactory ?? new LoggerFactoryStub();
            var clientFactory = new KeyManagementClientFactoryStub(options);

            return KeyManagementService.NewBuilder()
                .WithLoggerFactory(loggerFactory)
                .WithOptions(options)
                .WithKmsClientFactory(clientFactory)
                .Build();
        }
    }
}
