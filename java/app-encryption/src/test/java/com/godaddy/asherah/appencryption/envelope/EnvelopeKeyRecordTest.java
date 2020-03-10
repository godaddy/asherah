package com.godaddy.asherah.appencryption.envelope;

import static org.junit.jupiter.api.Assertions.*;

import java.time.Instant;
import java.time.temporal.ChronoUnit;
import java.util.Base64;
import java.util.Optional;

import org.json.JSONObject;
import org.junit.jupiter.api.Test;

import com.godaddy.asherah.appencryption.utils.Json;

class EnvelopeKeyRecordTest {
  private final Instant created = Instant.now().truncatedTo(ChronoUnit.SECONDS);
  private final String parentKey = "key1";
  private final Instant parentCreated = created.minus(1, ChronoUnit.SECONDS);
  private final KeyMeta parentKeyMeta = new KeyMeta(parentKey, parentCreated);
  private final byte[] encryptedKey = new byte[] {0, 1, 2, 3, 4};
  private final boolean revoked = true;

  @Test
  void testConstructorRegularWithoutRevokedShouldBeOptionalEmpty() {
    EnvelopeKeyRecord record = new EnvelopeKeyRecord(created, parentKeyMeta, encryptedKey);
    assertNotNull(record);
    assertEquals(created, record.getCreated());
    assertEquals(Optional.of(parentKeyMeta), record.getParentKeyMeta());
    assertArrayEquals(encryptedKey, record.getEncryptedKey());
    assertEquals(Optional.empty(), record.isRevoked());
  }

  @Test
  void testConstructorRegularWithParentKeyMetaAndRevoked() {
    EnvelopeKeyRecord record = new EnvelopeKeyRecord(created, parentKeyMeta, encryptedKey, revoked);
    assertNotNull(record);
    assertEquals(created, record.getCreated());
    assertEquals(Optional.of(parentKeyMeta), record.getParentKeyMeta());
    assertArrayEquals(encryptedKey, record.getEncryptedKey());
    assertEquals(Optional.of(revoked), record.isRevoked());
  }

  @Test
  void testConstructorRegularWithNullParentKeyMetaAndNullRevokedShouldBeOptionalEmpty() {
    EnvelopeKeyRecord record = new EnvelopeKeyRecord(created, null, encryptedKey, null);
    assertNotNull(record);
    assertEquals(created, record.getCreated());
    assertEquals(Optional.empty(), record.getParentKeyMeta());
    assertArrayEquals(encryptedKey, record.getEncryptedKey());
    assertEquals(Optional.empty(), record.isRevoked());
  }

  @Test
  void testConstructorJsonWithParentKeyMetaAndRevoked() {
    // yes, i know this is exposing bad practice. meh
    Json parentKeyMetaJson = new Json();
    parentKeyMetaJson.put("KeyId", parentKey);
    parentKeyMetaJson.put("Created", parentCreated);

    Json envelopeKeyRecordJson = new Json();
    envelopeKeyRecordJson.put("Created", created);
    envelopeKeyRecordJson.put("ParentKeyMeta", parentKeyMetaJson);
    envelopeKeyRecordJson.put("Key", encryptedKey);
    envelopeKeyRecordJson.put("Revoked", revoked);

    EnvelopeKeyRecord record = new EnvelopeKeyRecord(envelopeKeyRecordJson);
    assertNotNull(record);
    assertEquals(created, record.getCreated());
    assertEquals(Optional.of(parentKeyMeta), record.getParentKeyMeta());
    assertArrayEquals(encryptedKey, record.getEncryptedKey());
    assertEquals(Optional.of(revoked), record.isRevoked());
  }

  @Test
  void testConstructorJsonWithNullParentKeyMetaAndNullRevokedShouldBeOptionalEmptyAndFalse() {
    Json envelopeKeyRecordJson = new Json();
    envelopeKeyRecordJson.put("Created", created);
    envelopeKeyRecordJson.put("Key", encryptedKey);

    EnvelopeKeyRecord record = new EnvelopeKeyRecord(envelopeKeyRecordJson);
    assertNotNull(record);
    assertEquals(created, record.getCreated());
    assertEquals(Optional.empty(), record.getParentKeyMeta());
    assertArrayEquals(encryptedKey, record.getEncryptedKey());
    assertEquals(Optional.of(false), record.isRevoked());
  }

  @Test
  void testToJsonWithParentKeyMetaAndRevoked() {
    EnvelopeKeyRecord envelopeKeyRecord = new EnvelopeKeyRecord(created, parentKeyMeta, encryptedKey, revoked);

    JSONObject recordJson = envelopeKeyRecord.toJson();
    assertEquals(created.getEpochSecond(), recordJson.get("Created"));
    // yes, i know this is exposing bad practice. meh
    assertEquals(parentCreated.getEpochSecond(), recordJson.getJSONObject("ParentKeyMeta").get("Created"));
    assertEquals(parentKey, recordJson.getJSONObject("ParentKeyMeta").getString("KeyId"));
    assertArrayEquals(encryptedKey, Base64.getDecoder().decode(recordJson.getString("Key")));
    assertEquals(revoked, recordJson.get("Revoked"));
  }

  @Test
  void testToJsonWithNullParentKeyMetaAndNullRevokedShouldBeNull() {
    EnvelopeKeyRecord envelopeKeyRecord = new EnvelopeKeyRecord(created, null, encryptedKey);

    JSONObject recordJson = envelopeKeyRecord.toJson();
    assertEquals(created.getEpochSecond(), recordJson.get("Created"));
    assertNull(recordJson.optJSONObject("ParentKeyMeta"));
    assertArrayEquals(encryptedKey, Base64.getDecoder().decode(recordJson.getString("Key")));
    assertNull(recordJson.opt("Revoked"));
  }

}
