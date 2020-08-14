using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using LanguageExt;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("AppEncryption.Tests")]

namespace GoDaddy.Asherah.AppEncryption.Util
{
    /// <summary>
    /// This is a wrapper over <see cref="JObject"/> that adds additional helper methods to store and retrieve data key
    /// and other metadata for Asherah.
    /// </summary>
    public class Json
    {
        private readonly JObject document;

        /// <summary>
        /// Initializes a new instance of the <see cref="Json"/> class.
        /// </summary>
        public Json()
        {
            document = new JObject();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Json"/> class from the provided <paramref name="jObject"/>.
        /// </summary>
        ///
        /// <param name="jObject">The <see cref="JObject"/> object to convert to <see cref="Json"/>.</param>
        public Json(JObject jObject)
        {
            document = jObject ?? throw new ArgumentException("jObject is null");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Json"/> class from the provided <see cref="byte"/> array.
        /// </summary>
        ///
        /// <param name="utf8Json">An array of bytes to be converted to <see cref="Json"/>.</param>
        public Json(byte[] utf8Json)
        {
            document = ConvertUtf8ToJson(utf8Json);
        }

        /// <summary>
        /// Gets the value within the <see cref="Json"/> object that is associated with a given key.
        /// </summary>
        ///
        /// <param name="key">The key whose value needs to be retrieved.</param>
        /// <returns>The value associated with the key.</returns>
        public Json GetJson(string key)
        {
            return new Json(document.GetValue(key).ToObject<JObject>());
        }

        /// <summary>
        /// Gets the <see cref="Json"/> associated with a given key.
        /// </summary>
        ///
        /// <param name="key">The key whose value needs to be retrieved.</param>
        /// <returns>An <see cref="Option{Json}"/> value, which is <see cref="Option{A}.None"/> if the key does not
        /// exist.</returns>
        public Option<Json> GetOptionalJson(string key)
        {
            return document.TryGetValue(key, out JToken result) ? new Json(result.ToObject<JObject>()) : Option<Json>.None;
        }

        /// <summary>
        /// Gets the string value associated with a given key.
        /// </summary>
        ///
        /// <param name="key">The key whose value needs to be retrieved.</param>
        /// <returns>The value associated with the key, as a string.</returns>
        public string GetString(string key)
        {
            return document.GetValue(key).ToObject<string>();
        }

        /// <summary>
        /// Converts the key into a newly-allocated byte array.
        /// </summary>
        ///
        /// <param name="key">The key whose value needs to be retrieved.</param>
        /// <returns>The value associated with the key, as a byte array.</returns>
        public byte[] GetBytes(string key)
        {
            return Convert.FromBase64String(document.GetValue(key).ToObject<string>());
        }

        /// <summary>
        /// Retrieves the long value associated with a given key and converts it to an instance of
        /// <see cref="DateTimeOffset"/> as the number of seconds that have elapsed since 1970-01-01T00:00:00Z.
        /// </summary>
        ///
        /// <param name="key">The key whose value needs to be retrieved.</param>
        /// <returns>The time associated with the key.</returns>
        public DateTimeOffset GetDateTimeOffset(string key)
        {
            long unixTime = document.GetValue(key).ToObject<long>();
            return DateTimeOffset.FromUnixTimeSeconds(unixTime);
        }

        /// <summary>
        /// Gets the boolean value associated with a given key.
        /// </summary>
        ///
        /// <param name="key">The key whose value needs to be retrieved.</param>
        /// <returns>An <see cref="Option{boolean}"/> value which is <see cref="Option{A}.None"/>if the key does not
        /// exist.</returns>
        public Option<bool> GetOptionalBoolean(string key)
        {
            return document.TryGetValue(key, out JToken result) ? result.ToObject<Option<bool>>() : Option<bool>.None;
        }

        /// <summary>
        /// Gets the <see cref="JArray"/> associated with a given key.
        /// </summary>
        ///
        /// <param name="key">The key whose value needs to be retrieved.</param>
        /// <returns>The value associated with the key.</returns>
        public JArray GetJsonArray(string key)
        {
            return document.GetValue(key).ToObject<JArray>();
        }

        /// <summary>
        /// Adds a new entry to the <see cref="Json"/> object where the values is a <see cref="DateTimeOffset"/> object.
        /// </summary>
        ///
        /// <param name="key">The key to add to the <see cref="Json"/> object.</param>
        /// <param name="dateTimeOffset">The value associated with the key.</param>
        public void Put(string key, DateTimeOffset dateTimeOffset)
        {
            document.Add(key, dateTimeOffset.ToUnixTimeSeconds());
        }

        /// <summary>
        /// Adds a new entry to the <see cref="Json"/> object where the value is a <see cref="string"/> type.
        /// </summary>
        ///
        /// <param name="key">The key to add to the <see cref="Json"/> object.</param>
        /// <param name="text">The value associated with the key.</param>
        public void Put(string key, string text)
        {
            document.Add(key, text);
        }

        /// <summary>
        /// Adds a new entry to the <see cref="Json"/> object where the value is a <see cref="byte"/> array.
        /// </summary>
        ///
        /// <param name="key">The key to add to the <see cref="Json"/> object.</param>
        /// <param name="bytes">The value associated with the key.</param>
        public void Put(string key, byte[] bytes)
        {
            document.Add(key, Convert.ToBase64String(bytes));
        }

        /// <summary>
        /// Adds a new entry to the <see cref="Json"/> object where the value is a <see cref="JObject"/> object.
        /// </summary>
        ///
        /// <param name="key">The key to add to the <see cref="Json"/> object.</param>
        /// <param name="jObject">The value associated with the key.</param>
        public void Put(string key, JObject jObject)
        {
            document.Add(key, jObject);
        }

        /// <summary>
        /// Adds a new entry to the <see cref="Json"/> object where the value is a <see cref="Json"/> object.
        /// </summary>
        ///
        /// <param name="key">The key to add to the <see cref="Json"/> object.</param>
        /// <param name="json">The value associated with the key.</param>
        public void Put(string key, Json json)
        {
            document.Add(key, json.ToJObject());
        }

        /// <summary>
        /// Adds a new entry to the <see cref="Json"/> object where the value is a boolean.
        /// </summary>
        ///
        /// <param name="key">The key to add to the <see cref="Json"/> object.</param>
        /// <param name="value">The value associated with the key.</param>
        public void Put(string key, bool value)
        {
            document.Add(key, value);
        }

        /// <summary>
        /// Adds a new entry to the <see cref="Json"/> object where the value is a <see cref="List"/> of
        /// <see cref="JObject"/>.
        /// </summary>
        ///
        /// <param name="key">The key to add to the <see cref="Json"/> object.</param>
        /// <param name="jsonList">The value associated with the key.</param>
        public void Put(string key, List<JObject> jsonList)
        {
            document.Add(key, JToken.FromObject(jsonList));
        }

        /// <summary>
        /// Converts the <see cref="Json"/> object to a string.
        /// </summary>
        ///
        /// <returns>The json document as a string.</returns>
        public string ToJsonString()
        {
            return document.ToString(Formatting.None);
        }

        /// <summary>
        /// Converts the <see cref="Json"/> object to a byte array.
        /// </summary>
        ///
        /// <returns>The json document as a byte array.</returns>
        public byte[] ToUtf8()
        {
            return ConvertJsonToUtf8(document);
        }

        /// <summary>
        /// Converts the <see cref="Json"/> object to a <see cref="JObject"/> object.
        /// </summary>
        ///
        /// <returns>The json document as a <see cref="JObject"/> object.</returns>
        public JObject ToJObject()
        {
            return document;
        }

        private static byte[] ConvertJsonToUtf8(JObject jObject)
        {
            // JObject.ToString(Formatting.None) appears to be more efficient than JsonConvert.SerializeObject
            string serializeObject = jObject.ToString(Formatting.None);
            return Encoding.UTF8.GetBytes(serializeObject);
        }

        private static JObject ConvertUtf8ToJson(byte[] utf8)
        {
            string bytesAsString = Encoding.UTF8.GetString(utf8);

            // JObject.Parse appears to be more efficient than JsonConvert.DeserializeObject
            return JObject.Parse(bytesAsString);
        }
    }
}
