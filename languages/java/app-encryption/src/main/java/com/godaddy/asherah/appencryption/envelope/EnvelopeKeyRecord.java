package com.godaddy.asherah.appencryption.envelope;

import java.time.Instant;
import java.util.Optional;

import org.json.JSONObject;

import com.godaddy.asherah.appencryption.utils.Json;

public class EnvelopeKeyRecord {
  private final Instant created;
  private final Optional<KeyMeta> parentKeyMeta;
  private final byte[] encryptedKey;
  private final Optional<Boolean> revoked;

  public EnvelopeKeyRecord(final Instant created, final KeyMeta parentKeyMeta, final byte[] encryptedKey) {
    this(created, parentKeyMeta, encryptedKey, null);
  }

  public EnvelopeKeyRecord(final Instant created, final KeyMeta parentKeyMeta, final byte[] encryptedKey,
      final Boolean revoked) {
    this.created = created;
    this.parentKeyMeta = Optional.ofNullable(parentKeyMeta);
    this.encryptedKey = encryptedKey;
    this.revoked = Optional.ofNullable(revoked);
  }

  EnvelopeKeyRecord(final Json sourceJson) {
    created = sourceJson.getInstant("Created");
    parentKeyMeta = sourceJson.getOptionalJson("ParentKeyMeta").map(KeyMeta::new);
    encryptedKey = sourceJson.getBytes("Key");
    revoked = sourceJson.getOptionalBoolean("Revoked");
  }

  public JSONObject toJson() {
    Json json = new Json();
    json.put("Created", created);
    parentKeyMeta.ifPresent(keyMeta -> json.put("ParentKeyMeta", keyMeta.toJson()));
    json.put("Key", encryptedKey);
    revoked.ifPresent(value -> json.put("Revoked", value));
    return json.toJsonObject();
  }

  public Instant getCreated() {
    return created;
  }

  public Optional<KeyMeta> getParentKeyMeta() {
    return parentKeyMeta;
  }

  public byte[] getEncryptedKey() {
    return encryptedKey;
  }

  public Optional<Boolean> isRevoked() {
    return revoked;
  }
}
