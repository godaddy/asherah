using System;
using App.Metrics;

namespace GoDaddy.Asherah.AppEncryption.Util
{
    public static class MetricsUtil
    {
        public const string AelMetricsPrefix = "ael";

        private static volatile IMetrics metricsInstance;

        public static IMetrics MetricsInstance
        {
            get
            {
                // Check against null since we have separate init process. There's technically a race condition with
                // SetMetricsInstance, but we're the only ones ever calling into this, so not bothering with locking.
                if (metricsInstance == null)
                {
                    throw new ArgumentNullException(nameof(metricsInstance), "metricsInstance not initialized");
                }

                return metricsInstance;
            }
        }

        public static void SetMetricsInstance(IMetrics metricsInstance)
        {
            MetricsUtil.metricsInstance = metricsInstance;
        }
    }
}
