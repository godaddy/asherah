using System;
using GoDaddy.Asherah.Crypto.ExtensionMethods;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.Crypto.ExtensionMethods
{
    [Collection("Logger Fixture collection")]
    public class DateTimeOffsetExtensionTests
    {
        private const int Zero = 0;
        private const int One = 1;

        [Fact]
        public void TestTruncate()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset actualDateTime = now.Truncate(TimeSpan.FromMinutes(1));
            Assert.Equal(Zero, actualDateTime.Second);
        }

        [Fact]
        public void TestTruncateWithZeroTimespan()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Assert.Equal(now, now.Truncate(TimeSpan.Zero));
        }

        [Fact]
        public void TestTruncateWithMaxDateTime()
        {
            DateTimeOffset now = DateTimeOffset.MaxValue;
            Assert.Equal(now, now.Truncate(TimeSpan.FromMinutes(One)));
        }

        [Fact]
        public void TestTruncateWithMinDateTime()
        {
            DateTimeOffset now = DateTimeOffset.MinValue;
            Assert.Equal(now, now.Truncate(TimeSpan.FromMinutes(One)));
        }
    }
}
