using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GoDaddy.Asherah.AppEncryption.Serialization
{
    /// <summary>
    /// JSON converter that converts Unix timestamp (long) to DateTimeOffset and vice versa.
    /// This handles the conversion between Unix seconds since epoch and DateTimeOffset.
    /// </summary>
    internal class UnixTimestampDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        /// <inheritdoc/>
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.Number)
            {
                throw new JsonException($"Unexpected token type: {reader.TokenType}. Expected Number.");
            }

            long unixTimestamp = reader.GetInt64();
            return DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            long unixTimestamp = value.ToUnixTimeSeconds();
            writer.WriteNumberValue(unixTimestamp);
        }
    }
}
