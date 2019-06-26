using System;

namespace GoDaddy.Asherah.Crypto.ExtensionMethods
{
    public static class DateTimeOffsetExtensions
    {
        /// <summary>
        /// Truncates the <code>dateTimeOffset</code> by the given <code>timeSpan</code>. Based off of the solution from
        /// https://stackoverflow.com/questions/1004698/how-to-truncate-milliseconds-off-of-a-net-datetime.
        /// </summary>
        /// <param name="dateTimeOffset">the <code>DateTimeOffset</code> to truncate</param>
        /// <param name="timeSpan">the <code>TimeSpan</code> to truncate by</param>
        /// <returns>The new truncated <code>DateTimeOffset</code></returns>
        public static DateTimeOffset Truncate(this DateTimeOffset dateTimeOffset, TimeSpan timeSpan)
        {
            if (timeSpan == TimeSpan.Zero)
            {
                return dateTimeOffset; // Or could throw an ArgumentException
            }

            if (dateTimeOffset == DateTimeOffset.MinValue || dateTimeOffset == DateTimeOffset.MaxValue)
            {
                return dateTimeOffset; // do not modify "guard" values
            }

            return dateTimeOffset.AddTicks(-(dateTimeOffset.Ticks % timeSpan.Ticks));
        }
    }
}
