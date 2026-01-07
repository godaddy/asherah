using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using GoDaddy.Asherah.AppEncryption.PlugIns.Aws.Kms;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.PlugIns.Aws.Kms
{
    [ExcludeFromCodeCoverage]
    public class KeyManagementServiceOptionsTests
    {
        [Fact]
        public void OptimizeByRegions_WithPriorityRegions_SortsCorrectly()
        {
            // Arrange
            var options = new KeyManagementServiceOptions
            {
                RegionKeyArns = new List<RegionKeyArn>
                {
                    new RegionKeyArn { Region = "us-west-2", KeyArn = "arn-us-west-2" },
                    new RegionKeyArn { Region = "us-east-1", KeyArn = "arn-us-east-1" },
                    new RegionKeyArn { Region = "eu-west-1", KeyArn = "arn-eu-west-1" }
                }
            };

            // Act
            var result = options.OptimizeByRegions("us-east-1", "us-west-2");

            // Assert
            Assert.Equal(3, result.RegionKeyArns.Count);
            Assert.Equal("us-east-1", result.RegionKeyArns[0].Region);
            Assert.Equal("us-west-2", result.RegionKeyArns[1].Region);
            Assert.Equal("eu-west-1", result.RegionKeyArns[2].Region);
        }

        [Fact]
        public void OptimizeByRegions_WithSinglePriorityRegion_SortsCorrectly()
        {
            // Arrange
            var options = new KeyManagementServiceOptions
            {
                RegionKeyArns = new List<RegionKeyArn>
                {
                    new RegionKeyArn { Region = "us-west-2", KeyArn = "arn-us-west-2" },
                    new RegionKeyArn { Region = "us-east-1", KeyArn = "arn-us-east-1" },
                    new RegionKeyArn { Region = "eu-west-1", KeyArn = "arn-eu-west-1" }
                }
            };

            // Act
            var result = options.OptimizeByRegions("us-east-1");

            // Assert
            Assert.Equal(3, result.RegionKeyArns.Count);
            Assert.Equal("us-east-1", result.RegionKeyArns[0].Region);
            Assert.Equal("us-west-2", result.RegionKeyArns[1].Region);
            Assert.Equal("eu-west-1", result.RegionKeyArns[2].Region);
        }

        [Fact]
        public void OptimizeByRegions_WithPartialPriorityRegions_PutsPriorityFirst()
        {
            // Arrange
            var options = new KeyManagementServiceOptions
            {
                RegionKeyArns = new List<RegionKeyArn>
                {
                    new RegionKeyArn { Region = "us-west-2", KeyArn = "arn-us-west-2" },
                    new RegionKeyArn { Region = "us-east-1", KeyArn = "arn-us-east-1" },
                    new RegionKeyArn { Region = "eu-west-1", KeyArn = "arn-eu-west-1" },
                    new RegionKeyArn { Region = "ap-southeast-1", KeyArn = "arn-ap-southeast-1" }
                }
            };

            // Act
            var result = options.OptimizeByRegions("eu-west-1", "ap-southeast-1");

            // Assert
            Assert.Equal(4, result.RegionKeyArns.Count);
            Assert.Equal("eu-west-1", result.RegionKeyArns[0].Region);
            Assert.Equal("ap-southeast-1", result.RegionKeyArns[1].Region);
            // Non-priority regions maintain original order
            Assert.Equal("us-west-2", result.RegionKeyArns[2].Region);
            Assert.Equal("us-east-1", result.RegionKeyArns[3].Region);
        }

        [Fact]
        public void OptimizeByRegions_WithNoMatchingPriorityRegions_MaintainsOriginalOrder()
        {
            // Arrange
            var options = new KeyManagementServiceOptions
            {
                RegionKeyArns = new List<RegionKeyArn>
                {
                    new RegionKeyArn { Region = "us-west-2", KeyArn = "arn-us-west-2" },
                    new RegionKeyArn { Region = "us-east-1", KeyArn = "arn-us-east-1" }
                }
            };

            // Act
            var result = options.OptimizeByRegions("eu-west-1", "ap-southeast-1");

            // Assert
            Assert.Equal(2, result.RegionKeyArns.Count);
            Assert.Equal("us-west-2", result.RegionKeyArns[0].Region);
            Assert.Equal("us-east-1", result.RegionKeyArns[1].Region);
        }

        [Fact]
        public void OptimizeByRegions_WithEmptyPriorityRegions_MaintainsOriginalOrder()
        {
            // Arrange
            var options = new KeyManagementServiceOptions
            {
                RegionKeyArns = new List<RegionKeyArn>
                {
                    new RegionKeyArn { Region = "us-west-2", KeyArn = "arn-us-west-2" },
                    new RegionKeyArn { Region = "us-east-1", KeyArn = "arn-us-east-1" }
                }
            };

            // Act
            var result = options.OptimizeByRegions();

            // Assert
            Assert.Equal(2, result.RegionKeyArns.Count);
            Assert.Equal("us-west-2", result.RegionKeyArns[0].Region);
            Assert.Equal("us-east-1", result.RegionKeyArns[1].Region);
        }

        [Fact]
        public void OptimizeByRegions_IsCaseInsensitive()
        {
            // Arrange
            var options = new KeyManagementServiceOptions
            {
                RegionKeyArns = new List<RegionKeyArn>
                {
                    new RegionKeyArn { Region = "us-west-2", KeyArn = "arn-us-west-2" },
                    new RegionKeyArn { Region = "US-EAST-1", KeyArn = "arn-us-east-1" },
                    new RegionKeyArn { Region = "Eu-West-1", KeyArn = "arn-eu-west-1" }
                }
            };

            // Act
            var result = options.OptimizeByRegions("us-east-1", "eu-west-1");

            // Assert
            Assert.Equal(3, result.RegionKeyArns.Count);
            Assert.Equal("US-EAST-1", result.RegionKeyArns[0].Region);
            Assert.Equal("Eu-West-1", result.RegionKeyArns[1].Region);
            Assert.Equal("us-west-2", result.RegionKeyArns[2].Region);
        }

        [Fact]
        public void OptimizeByRegions_ReturnsNewInstance_DoesNotModifyOriginal()
        {
            // Arrange
            var options = new KeyManagementServiceOptions
            {
                RegionKeyArns = new List<RegionKeyArn>
                {
                    new RegionKeyArn { Region = "us-west-2", KeyArn = "arn-us-west-2" },
                    new RegionKeyArn { Region = "us-east-1", KeyArn = "arn-us-east-1" }
                }
            };
            var originalOrder = options.RegionKeyArns.Select(rka => rka.Region).ToList();

            // Act
            var result = options.OptimizeByRegions("us-east-1");

            // Assert
            Assert.NotSame(options, result);
            Assert.Equal(originalOrder, options.RegionKeyArns.Select(rka => rka.Region).ToList());
            Assert.Equal("us-east-1", result.RegionKeyArns[0].Region);
        }

        [Fact]
        public void OptimizeByRegions_WithDuplicatePriorityRegions_HandlesCorrectly()
        {
            // Arrange
            var options = new KeyManagementServiceOptions
            {
                RegionKeyArns = new List<RegionKeyArn>
                {
                    new RegionKeyArn { Region = "us-west-2", KeyArn = "arn-us-west-2" },
                    new RegionKeyArn { Region = "us-east-1", KeyArn = "arn-us-east-1" },
                    new RegionKeyArn { Region = "eu-west-1", KeyArn = "arn-eu-west-1" }
                }
            };

            // Act
            var result = options.OptimizeByRegions("us-east-1", "us-east-1", "us-west-2");

            // Assert
            Assert.Equal(3, result.RegionKeyArns.Count);
            // First occurrence in priority list determines position
            Assert.Equal("us-east-1", result.RegionKeyArns[0].Region);
            Assert.Equal("us-west-2", result.RegionKeyArns[1].Region);
            Assert.Equal("eu-west-1", result.RegionKeyArns[2].Region);
        }

        [Fact]
        public void OptimizeByRegions_WithNullOrEmptyPriorityRegions_IgnoresThem()
        {
            // Arrange
            var options = new KeyManagementServiceOptions
            {
                RegionKeyArns = new List<RegionKeyArn>
                {
                    new RegionKeyArn { Region = "us-west-2", KeyArn = "arn-us-west-2" },
                    new RegionKeyArn { Region = "us-east-1", KeyArn = "arn-us-east-1" }
                }
            };

            // Act
            var result = options.OptimizeByRegions("us-east-1", null, "", "us-west-2");

            // Assert
            Assert.Equal(2, result.RegionKeyArns.Count);
            Assert.Equal("us-east-1", result.RegionKeyArns[0].Region);
            Assert.Equal("us-west-2", result.RegionKeyArns[1].Region);
        }
    }
}
