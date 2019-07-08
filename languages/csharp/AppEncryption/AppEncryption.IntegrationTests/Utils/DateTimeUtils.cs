using System;

namespace GoDaddy.Asherah.AppEncryption.IntegrationTests.Utils
{
    public static class DateTimeUtils
    {
        /// <summary>
        /// Returns current UTC time in ISO 8601 format
        /// </summary>
        /// <returns>A string which contains the Datetime in ISO 8601 format</returns>
        public static string GetCurrentTimeAsUtcIsoDateTimeOffset()
        {
            return DateTimeOffset.UtcNow.ToString("o");
        }

        /// <summary>
        /// Returns a datetime converted to ISO 8601 format
        /// </summary>
        /// <param name="dateTimeOffset">A DateTimeOffset object that need to be converted to ISO 8601 format</param>
        /// <returns>A string which contains the input DateTime in ISO 8601 format</returns>
        public static string GetDateTimeAsUtcIsoOffsetDateTime(DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString("o");
        }
    }
}
