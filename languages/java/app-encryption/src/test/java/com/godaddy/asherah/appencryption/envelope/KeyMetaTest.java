package com.godaddy.asherah.appencryption.envelope;

import static org.junit.jupiter.api.Assertions.*;

import java.time.Instant;
import java.time.temporal.ChronoUnit;

import org.json.JSONObject;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import com.godaddy.asherah.appencryption.utils.Json;

class KeyMetaTest {
  private final String keyId = "key1";
  private final Instant created = Instant.now().truncatedTo(ChronoUnit.SECONDS);

  private KeyMeta keyMeta;

  @BeforeEach
  void setUp(){
    keyMeta = new KeyMeta(keyId, created);
  }

  @Test
  void testConstructorRegular() {
    KeyMeta meta = new KeyMeta(keyId, created);
    assertNotNull(meta);
    assertEquals(keyId, meta.getKeyId());
    assertEquals(created, meta.getCreated());
  }

  @Test
  void testConstructorJson() {
    Json keyMetaJson = new Json();
    keyMetaJson.put("KeyId", keyId);
    keyMetaJson.put("Created", created);

    KeyMeta meta = new KeyMeta(keyMetaJson);
    assertNotNull(meta);
    assertEquals(keyId, meta.getKeyId());
    assertEquals(created, meta.getCreated());
  }

  @Test
  void testToJson() {
    JSONObject metaJson = keyMeta.toJson();
    assertEquals(keyId, metaJson.getString("KeyId"));
    assertEquals(created.getEpochSecond(), metaJson.getLong("Created"));
  }

  @Test
  void testHashCodeAndEqualsSymmetric() {
    KeyMeta otherKeyMeta = new KeyMeta(keyId, created);
    assertTrue(keyMeta.hashCode() == otherKeyMeta.hashCode());
    assertNotSame(keyMeta, otherKeyMeta);
    assertTrue(keyMeta.equals(otherKeyMeta) && otherKeyMeta.equals(keyMeta));
  }

  @Test
  void testEqualsWithSameInstance() {
    assertTrue(keyMeta.equals(keyMeta));
  }

  @Test
  void testEqualsWithNull() {
    assertFalse(keyMeta.equals(null));
  }

  @SuppressWarnings("unlikely-arg-type")
  @Test
  void testEqualsWithDifferentClass() {
    assertFalse(keyMeta.equals("blah"));
  }

  @Test
  void testEqualsWithDifferentKeyId() {
    KeyMeta otherKeyMeta = new KeyMeta("some_other_keyid", created);
    assertFalse(keyMeta.equals(otherKeyMeta));
    assertFalse(otherKeyMeta.equals(keyMeta));
  }

  @Test
  void testEqualsWithDifferentCreated() {
    KeyMeta otherKeyMeta = new KeyMeta(keyId, created.minus(1, ChronoUnit.MINUTES));
    assertFalse(keyMeta.equals(otherKeyMeta));
    assertFalse(otherKeyMeta.equals(keyMeta));
  }

}
