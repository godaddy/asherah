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

  /**
   * Constructor for EnvelopeKeyRecord.
   * @param created creation time of the {@code EnvelopeKeyRecord}.
   * @param parentKeyMeta the {@link KeyMeta} for encryption keys.
   * @param encryptedKey the encrypted key.
   */
  public EnvelopeKeyRecord(final Instant created, final KeyMeta parentKeyMeta, final byte[] encryptedKey) {
    this(created, parentKeyMeta, encryptedKey, null);
  }

  /**
   * Constructor for EnvelopeKeyRecord.
   * @param created creation time of the {@code EnvelopeKeyRecord}.
   * @param parentKeyMeta the {@link KeyMeta} for encryption keys.
   * @param encryptedKey the encrypted key.
   * @param revoked the revocation status of the encrypted key.
   */
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

  /**
   * Converts the {@link EnvelopeKeyRecord} to a @link org.json.JSONObject}.
   * @return a {@link org.json.JSONObject} object.
   */
  public JSONObject toJson() {
    Json json = new Json();
    json.put("Created", created);
    parentKeyMeta.ifPresent(keyMeta -> json.put("ParentKeyMeta", keyMeta.toJson()));
    json.put("Key", encryptedKey);
    revoked.ifPresent(value -> json.put("Revoked", value));
    return json.toJsonObject();
  }

  /**
   * Getter for the field <code>created</code>.
   * @return the creation timestamp of the EKR
   */
  public Instant getCreated() {
    return created;
  }

  /**
   * Getter for the field <code>parentKeyMeta</code>.
   * @return the parent key meta
   */
  public Optional<KeyMeta> getParentKeyMeta() {
    return parentKeyMeta;
  }

  /**
   * Getter for the field <code>encryptedKey</code>.
   * @return the encrypted key
   */
  public byte[] getEncryptedKey() {
    return encryptedKey;
  }

  /**
   * Checks if the key is revoked.
   * @return  <code>true</code> if key is revoked, else null.
   */
  public Optional<Boolean> isRevoked() {
    return revoked;
  }
}
