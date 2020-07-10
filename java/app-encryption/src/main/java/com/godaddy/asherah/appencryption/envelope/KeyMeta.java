package com.godaddy.asherah.appencryption.envelope;

import java.time.Instant;
import java.util.Objects;

import org.json.JSONObject;

import com.godaddy.asherah.appencryption.utils.Json;

/**
 * The {@code KeyMeta} format is below.
 * <pre>
 * {
 *   KeyId: "some_key_id",
 *   Created: 1534553054
 * }
 * </pre>
 */
public class KeyMeta {
  private final String keyId;
  private final Instant created;

  /**
   * Creates a new {@code KeyMeta} instance using the provided parameters. {@code KeyMeta} is the metadata in
   * {@link EnvelopeKeyRecord} that references a parent key in the key hierarchy.
   * Note that for system keys, this content may be embedded within the encrypted key content, depending on the KMS
   * being used.
   *
   * @param keyId The key Id.
   * @param created The creation time of the key.
   */
  public KeyMeta(final String keyId, final Instant created) {
    this.keyId = keyId;
    this.created = created;
  }

  KeyMeta(final Json sourceJson) {
    keyId = sourceJson.getString("KeyId");
    created = sourceJson.getInstant("Created");
  }

  /**
   * Converts the {@code KeyMeta} to a {@link org.json.JSONObject} with below format.
   * <pre>
   * {
   *   "KeyId": "some_key_id",
   *   "Created": 1534553054
   * }
   * </pre>
   *
   * @return The {@code KeyMeta} converted to a {@link org.json.JSONObject} object.
   */
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
