using System;
using App.Metrics;

namespace GoDaddy.Asherah.AppEncryption.Util
{
    /// <summary>
    /// A helper class that sets the metrics instance to be used.
    /// </summary>
    public static class MetricsUtil
    {
        /// <summary>
        /// The metrics prefix to use for all counters. This prefix is to be used throughout the SDK.
        /// </summary>
        public const string AelMetricsPrefix = "ael";

        private static volatile IMetrics metricsInstance;

        /// <summary>
        /// Gets the defined metrics instance. It follows a singleton pattern, so that only one instance is initialized.
        /// </summary>
        ///
        /// <exception cref="ArgumentNullException">If no <see cref="IMetrics"/> instance has been
        /// initialized.</exception>
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

        /// <summary>
        /// Sets the <see cref="metricsInstance"/> to be used using the provided parameter.
        /// </summary>
        ///
        /// <param name="metricsInstance">The <see cref="IMetrics"/> instance to use with the SDK.</param>
        public static void SetMetricsInstance(IMetrics metricsInstance)
        {
            MetricsUtil.metricsInstance = metricsInstance;
        }
    }
}
