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
    public class Json
    {
        private readonly JObject document;

        public Json()
        {
            document = new JObject();
        }

        public Json(JObject jObject)
        {
            document = jObject ?? throw new ArgumentException("jObject is null");
        }

        public Json(byte[] utf8Json)
        {
            document = ConvertUtf8ToJson(utf8Json);
        }

        public Json GetJson(string key)
        {
            return new Json(document.GetValue(key).ToObject<JObject>());
        }

        public Option<Json> GetOptionalJson(string key)
        {
            return document.TryGetValue(key, out JToken result) ? new Json(result.ToObject<JObject>()) : Option<Json>.None;
        }

        public string GetString(string key)
        {
            return document.GetValue(key).ToObject<string>();
        }

        public byte[] GetBytes(string key)
        {
            return Convert.FromBase64String(document.GetValue(key).ToObject<string>());
        }

        public DateTimeOffset GetDateTimeOffset(string key)
        {
            long unixTime = document.GetValue(key).ToObject<long>();
            return DateTimeOffset.FromUnixTimeSeconds(unixTime);
        }

        public Option<bool> GetOptionalBoolean(string key)
        {
            return document.TryGetValue(key, out JToken result) ? result.ToObject<Option<bool>>() : Option<bool>.None;
        }

        public JArray GetJsonArray(string key)
        {
            return document.GetValue(key).ToObject<JArray>();
        }

        public void Put(string key, DateTimeOffset dateTimeOffset)
        {
            document.Add(key, dateTimeOffset.ToUnixTimeSeconds());
        }

        public void Put(string key, string text)
        {
            document.Add(key, text);
        }

        public void Put(string key, byte[] bytes)
        {
            document.Add(key, Convert.ToBase64String(bytes));
        }

        public void Put(string key, JObject jObject)
        {
            document.Add(key, jObject);
        }

        public void Put(string key, Json json)
        {
            document.Add(key, json.ToJObject());
        }

        public void Put(string key, bool value)
        {
            document.Add(key, value);
        }

        public void Put(string key, List<JObject> jsonList)
        {
            document.Add(key, JToken.FromObject(jsonList));
        }

        public string ToJsonString()
        {
            return document.ToString(Formatting.None);
        }

        public byte[] ToUtf8()
        {
            return ConvertJsonToUtf8(document);
        }

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
