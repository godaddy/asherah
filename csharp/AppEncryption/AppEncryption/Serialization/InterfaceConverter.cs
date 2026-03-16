using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GoDaddy.Asherah.AppEncryption.Serialization
{
    /// <summary>
    /// Generic JSON converter that handles serialization/deserialization between concrete types and their interfaces.
    /// This allows JSON deserialization to work with interface types by specifying the concrete implementation.
    /// </summary>
    ///
    /// <typeparam name="TConcrete">The concrete type that implements the interface.</typeparam>
    /// <typeparam name="TInterface">The interface type.</typeparam>
    internal class InterfaceConverter<TConcrete, TInterface> : JsonConverter<TInterface>
        where TConcrete : class, TInterface
    {
        /// <inheritdoc/>
        public override TInterface Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize<TConcrete>(ref reader, options);
        }

        public override void Write(Utf8JsonWriter writer, TInterface value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
