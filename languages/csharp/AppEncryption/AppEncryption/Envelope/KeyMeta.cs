using System;
using GoDaddy.Asherah.AppEncryption.Util;
using Newtonsoft.Json.Linq;

namespace GoDaddy.Asherah.AppEncryption.Envelope
{
    public class KeyMeta
    {
        public KeyMeta(string keyId, DateTimeOffset created)
        {
            KeyId = keyId;
            Created = created;
        }

        public KeyMeta(Json sourceJson)
        {
            KeyId = sourceJson.GetString("KeyId");
            Created = sourceJson.GetDateTimeOffset("Created");
        }

        internal string KeyId { get; }

        internal virtual DateTimeOffset Created { get; }

        public JObject ToJson()
        {
            Json json = new Json();
            json.Put("KeyId", KeyId);
            json.Put("Created", Created);
            return json.ToJObject();
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }

            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            KeyMeta other = (KeyMeta)obj;
            return Equals(KeyId, other.KeyId)
                   && Equals(Created, other.Created);
        }

        public override int GetHashCode()
        {
            return (KeyId, Created).GetHashCode();
        }

        public override string ToString()
        {
            return "KeyMeta [KeyId=" + KeyId + ", Created=" + Created + "]";
        }
    }
}
