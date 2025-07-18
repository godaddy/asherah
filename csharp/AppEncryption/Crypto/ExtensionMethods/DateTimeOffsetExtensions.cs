using System;

namespace GoDaddy.Asherah.Crypto.ExtensionMethods
{
  public static class DateTimeOffsetExtensions
  {
    /// <summary>
    /// Truncates the <see cref="dateTimeOffset"/> by the given <see cref="timeSpan"/>. Based off of the solution
    /// from https://stackoverflow.com/questions/1004698/how-to-truncate-milliseconds-off-of-a-net-datetime.
    /// </summary>
    /// <param name="dateTimeOffset">the <see cref="DateTimeOffset"/> to truncate.</param>
    /// <param name="timeSpan">the <see cref="TimeSpan"/> to truncate by.</param>
    /// <returns>The new truncated <see cref="DateTimeOffset"/>.</returns>
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
