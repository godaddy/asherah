using System;
using System.Text;
using LanguageExt;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GoDaddy.Asherah.AppEncryption.Tests.AppEncryption.Util
{
    public class JsonTest
    {
        private readonly Asherah.AppEncryption.Util.Json testDocument;

        public JsonTest()
        {
            testDocument = new Asherah.AppEncryption.Util.Json();
        }

        [Fact]
        public void TestJsonDateParsing()
        {
            string time = DateTime.UtcNow.ToString("o");
            JObject jObject = new JObject
            {
                { "Created", 1541461380 },
                { "Time", time },
            };

            // Get json bytes
            byte[] jsonBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jObject));

            // Convert to JObject using the Asherah.AppEncryption.Util.Json class. This in turn calls the
            // ConvertUtf8ToJson method which sets the DateParseHandling to None
            JObject json = new Asherah.AppEncryption.Util.Json(jsonBytes).ToJObject();

            Assert.Equal(time, json.GetValue("Time").ToString());
        }

        [Fact]
        private void TestGetDateTimeOffsetRoundTrip()
        {
            string key = "testDateTime";

            DateTimeOffset expectedDateTimeOffset = new DateTimeOffset(2019, 3, 21, 23, 24, 0, TimeSpan.Zero);

            testDocument.Put(key, expectedDateTimeOffset);
            DateTimeOffset actualDateTimeOffset = testDocument.GetDateTimeOffset(key);

            Assert.Equal(expectedDateTimeOffset, actualDateTimeOffset);
        }

        [Fact]
        private void TestGetDateTimeOffsetWithDateTimeKindLocal()
        {
            string key = "testDateTime";
            DateTimeOffset expectedDateTime =
                new DateTimeOffset(new DateTime(2019, 3, 21, 23, 24, 0, DateTimeKind.Local));

            testDocument.Put(key, expectedDateTime);
            DateTimeOffset actualDateTime = testDocument.GetDateTimeOffset(key);

            Assert.Equal(expectedDateTime, actualDateTime);
        }

        [Fact]
        private void TestGetDateTimeOffsetWithDateTimeKindUnspecified()
        {
            string key = "testDateTime";
            DateTimeOffset expectedDateTime =
                new DateTimeOffset(new DateTime(2019, 3, 21, 23, 24, 0, DateTimeKind.Unspecified));

            testDocument.Put(key, expectedDateTime);
            DateTimeOffset actualDateTime = testDocument.GetDateTimeOffset(key);

            Assert.Equal(expectedDateTime, actualDateTime);
        }

        [Fact]
        private void TestGetDateTimeOffsetWithDateTimeKindUtc()
        {
            string key = "testDateTime";

            DateTimeOffset expectedDateTime =
                new DateTimeOffset(new DateTime(2019, 3, 21, 23, 24, 0, DateTimeKind.Utc));

            testDocument.Put(key, expectedDateTime);
            DateTimeOffset actualDateTime = testDocument.GetDateTimeOffset(key);

            Assert.Equal(expectedDateTime, actualDateTime);
        }

        [Fact]
        private void TestDateTimeOffsetSerialization()
        {
            string key = "testDateTime";
            string unixTimeStampString = "1553210640"; // March 21, 2019 11:24:00 PM GMT
            string expectedSerializedObject = string.Concat("{\"", key, "\":", unixTimeStampString, "}");

            DateTimeOffset dateTimeOffset = new DateTimeOffset(2019, 3, 21, 23, 24, 0, TimeSpan.Zero);
            testDocument.Put(key, dateTimeOffset);

            string actualSerializedObject = testDocument.ToJsonString();

            Assert.Equal(expectedSerializedObject, actualSerializedObject);
        }

        [Fact]
        private void TestDateTimeOffsetDeserialization()
        {
            string key = "testDateTime";
            string unixTimeStampString = "1553210640"; // March 21, 2019 11:24:00 PM GMT
            string jsonString = string.Concat("{\"", key, "\":", unixTimeStampString, "}");
            DateTimeOffset expectedDateTimeOffset = new DateTimeOffset(2019, 3, 21, 23, 24, 0, TimeSpan.Zero);

            Asherah.AppEncryption.Util.Json json = new Asherah.AppEncryption.Util.Json(JObject.Parse(jsonString));
            DateTimeOffset actualDateTimeOffset = json.GetDateTimeOffset(key);

            Assert.Equal(expectedDateTimeOffset, actualDateTimeOffset);
        }

        /**
         * Known byte array used {0, 1, 2, 3, 4}
         * Serializes the value as `AAECAwQ=`
         * Method to verify if cross-platform works correctly
         */
        [Fact]
        private void TestGetBytes()
        {
            byte[] expectedBytes = { 0, 1, 2, 3, 4 };

            testDocument.Put("test_bytes", Convert.FromBase64String("AAECAwQ="));

            Assert.Equal(expectedBytes, testDocument.GetBytes("test_bytes"));
        }

        [Fact]
        private void TestGetOptionalBooleanWhenKeyDoesntExist()
        {
            testDocument.Put("test_bytes", Convert.FromBase64String("AAECAwQ="));
            Assert.Equal(Option<bool>.None, testDocument.GetOptionalBoolean("Revoked"));
        }

        [Fact]
        private void TestGetOptionalBooleanWhenKeyExists()
        {
            testDocument.Put("Revoked", false);
            Assert.Equal(Option<bool>.Some(false), testDocument.GetOptionalBoolean("Revoked"));
        }

        [Fact]
        private void TestGetOptionalJsonWhenKeyDoesntExist()
        {
            testDocument.Put("test_bytes", Convert.FromBase64String("AAECAwQ="));
            Assert.Equal(Option<Asherah.AppEncryption.Util.Json>.None, testDocument.GetOptionalJson("ParentKeyMeta"));
        }

        [Fact]
        private void TestGetOptionalJsonWhenKeyExists()
        {
            const string json = @"{key:'some_key', value:123}";
            JObject someMeta = JObject.Parse(json);
            testDocument.Put("ParentKeyMeta", someMeta);
            Option<Asherah.AppEncryption.Util.Json> resultJson = testDocument.GetOptionalJson("ParentKeyMeta");
            Assert.True(JToken.DeepEquals(someMeta, resultJson.Map(json1 => json1.ToJObject()).IfNone(new JObject())));
        }
    }
}
