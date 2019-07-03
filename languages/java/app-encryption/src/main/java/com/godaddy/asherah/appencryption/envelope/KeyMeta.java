package com.godaddy.asherah.appencryption.envelope;

import java.time.Instant;
import java.util.Objects;

import org.json.JSONObject;

import com.godaddy.asherah.appencryption.utils.Json;

public class KeyMeta {
  private final String keyId;
  private final Instant created;

  public KeyMeta(final String keyId, final Instant created) {
    this.keyId = keyId;
    this.created = created;
  }

  KeyMeta(final Json sourceJson) {
    keyId = sourceJson.getString("KeyId");
    created = sourceJson.getInstant("Created");
  }

  public JSONObject toJson() {
    Json json = new Json();
    json.put("KeyId", keyId);
    json.put("Created", created);
    return json.toJsonObject();
  }

  public String getKeyId() {
    return keyId;
  }

  public Instant getCreated() {
    return created;
  }

  @Override
  public int hashCode() {
    return Objects.hash(keyId, created);
  }

  @Override
  public boolean equals(final Object obj) {
    if (this == obj) {
      return true;
    }
    if (obj == null || getClass() != obj.getClass()) {
      return false;
    }

    KeyMeta other = (KeyMeta) obj;
    return Objects.equals(keyId, other.keyId)
        && Objects.equals(created, other.created);
  }

  @Override
  public String toString() {
    return "KeyMeta [keyId=" + keyId + ", created=" + created + "]";
  }

}
