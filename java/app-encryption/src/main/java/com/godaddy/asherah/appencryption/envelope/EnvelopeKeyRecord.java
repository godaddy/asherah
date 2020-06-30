package com.godaddy.asherah.appencryption.envelope;

import java.time.Instant;
import java.util.Optional;

import org.json.JSONObject;

import com.godaddy.asherah.appencryption.utils.Json;

/**
 * The {@code EnvelopeKeyRecord} format is:
 * <pre>
 * {
 *   Created: UTC epoch in seconds of when the key was created,
 *   // Identifier data of parent key (which encrypts this key)
 *   ParentKeyMeta: {
 *     KeyId: KeyId of the parent key,
 *     Created: Created timestamp of parent key,
 *   },
 *   Key: Base64 converted value of "Key encrypted with the parent key",
 *   Revoked: The revocation status of the key (True/False)
 * }
 * </pre>
 *  NOTE: For system key, the parent {@link KeyMeta} (in this case the master key identifier) may instead be a
 *  part of the Key content, depending on the master key type.
 */
public class EnvelopeKeyRecord {
  private final Instant created;
  private final Optional<KeyMeta> parentKeyMeta;
  private final byte[] encryptedKey;
  private final Optional<Boolean> revoked;

  /**
   * Creates an {@code EnvelopeKeyRecord} instance using the provided parameters. An envelope key record is an internal
   * data structure used to represent a system key, intermediate key or a data row key. It consists of an encrypted key
   * and metadata referencing the parent key in the key hierarchy used to encrypt it (i.e. its Key Encryption Key).
   *
   * @param created Creation time of the {@code encryptedKey}.
   * @param parentKeyMeta The {@link KeyMeta} for encryption keys.
   * @param encryptedKey The encrypted key (a system key, intermediate key or a data row key).
   */
  public EnvelopeKeyRecord(final Instant created, final KeyMeta parentKeyMeta, final byte[] encryptedKey) {
    this(created, parentKeyMeta, encryptedKey, null);
  }

  /**
   * Creates an {@code EnvelopeKeyRecord} instance using the provided parameters. An envelope key record is an internal
   * data structure used to represent a system key, intermediate key or a data row key. It consists of an encrypted key
   * and metadata referencing the parent key in the key hierarchy used to encrypt it (i.e. its Key Encryption Key).
   *
   * @param created Creation time of the {@code encryptedKey}.
   * @param parentKeyMeta The {@link KeyMeta} for encryption keys.
   * @param encryptedKey The encrypted key (a system key, intermediate key or a data row key).
   * @param revoked The revocation status of the encrypted key.
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
   * Converts The {@code EnvelopeKeyRecord} to a {@link org.json.JSONObject} with below format.
   * <pre>
   * {
   *   "Created": Creation time of the encrypted key,
   *   "ParentKeyMeta" : Parent key meta of the encrypted key(if present),
   *   "Key": Encrypted key,
   *   "Revoked": True/False
   * }
   * </pre>
   *
   * @return The {@code EnvelopeKeyRecord} converted to a {@link org.json.JSONObject} object.
   */
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

  /**
   * Checks if the key in the {@code EnvelopeKeyRecord} is revoked.
   *
   * @return {@code true} if key is revoked.
   */
  public Optional<Boolean> isRevoked() {
    return revoked;
  }
}
