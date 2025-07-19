using App.Metrics;
using GoDaddy.Asherah.AppEncryption.Util;

namespace GoDaddy.Asherah.AppEncryption.Tests
{
    // NOTE: This has to be used as class fixture since one of the MetricsUtil unit tests sets instance to null
    public class MetricsFixture
    {
        public MetricsFixture()
        {
            // Sets default builder to initialize properly for classes under test that use metrics.
            MetricsUtil.SetMetricsInstance(AppMetrics.CreateDefaultBuilder().Build());
        }
    }
}
