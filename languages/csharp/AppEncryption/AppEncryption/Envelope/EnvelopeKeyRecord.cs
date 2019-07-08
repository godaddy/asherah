using System;
using System.Runtime.CompilerServices;
using GoDaddy.Asherah.AppEncryption.Util;
using LanguageExt;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("AppEncryption.IntegrationTests")]

namespace GoDaddy.Asherah.AppEncryption.Envelope
{
    public class EnvelopeKeyRecord
    {
        public EnvelopeKeyRecord(DateTimeOffset created,  KeyMeta parentKeyMeta,  byte[] encryptedKey)
            : this(created, parentKeyMeta, encryptedKey, null)
        {
        }

        public EnvelopeKeyRecord(DateTimeOffset created,  KeyMeta parentKeyMeta,  byte[] encryptedKey,  bool? revoked)
        {
            Created = created;
            ParentKeyMeta = parentKeyMeta;
            EncryptedKey = encryptedKey;
            Revoked = revoked ?? Option<bool>.None;
        }

        public EnvelopeKeyRecord(Json sourceJson)
        {
            Created = sourceJson.GetDateTimeOffset("Created");
            ParentKeyMeta = sourceJson.GetOptionalJson("ParentKeyMeta").Map(x => new KeyMeta(x));
            EncryptedKey = sourceJson.GetBytes("Key");
            Revoked = sourceJson.GetOptionalBoolean("Revoked");
        }

        internal DateTimeOffset Created { get; }

        internal Option<KeyMeta> ParentKeyMeta { get; }

        internal Option<bool> Revoked { get; }

        internal byte[] EncryptedKey { get; }

        public JObject ToJson()
        {
            Json json = new Json();
            json.Put("Created", Created);
            json.Put("Key", EncryptedKey);
            ParentKeyMeta.IfSome(keyMeta => json.Put("ParentKeyMeta", keyMeta.ToJson()));
            Revoked.IfSome(revoked => json.Put("Revoked", revoked));
            return json.ToJObject();
        }
    }
}
