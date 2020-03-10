using System;
using App.Metrics;
using GoDaddy.Asherah.AppEncryption.Util;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Util
{
    [Collection("Logger Fixture collection")]
    public class MetricsUtilTest
    {
        [Fact]
        private void TestMetricsInstance()
        {
            IMetrics metrics = AppMetrics.CreateDefaultBuilder().Build();
            MetricsUtil.SetMetricsInstance(metrics);

            Assert.Equal(metrics, MetricsUtil.MetricsInstance);
        }

        [Fact]
        private void TestMetricsInstanceWithNull()
        {
            MetricsUtil.SetMetricsInstance(null);

            Assert.Throws<ArgumentNullException>(() => MetricsUtil.MetricsInstance);
        }
    }
}
