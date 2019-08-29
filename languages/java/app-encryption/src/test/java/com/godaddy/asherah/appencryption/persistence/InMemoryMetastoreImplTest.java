package com.godaddy.asherah.appencryption.persistence;

import static org.junit.jupiter.api.Assertions.*;

import java.time.Instant;
import java.time.temporal.ChronoUnit;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

class InMemoryMetastoreImplTest {
  InMemoryMetastoreImpl<String> inMemoryMetastoreImpl;

  @BeforeEach
  void setUp() {
    inMemoryMetastoreImpl = new InMemoryMetastoreImpl<>();
  }

  @Test
  void testLoadAndStoreWithValidKey() {
    String keyId = "ThisIsMyKey";
    Instant created = Instant.now();
    String value = "This is my value";

    inMemoryMetastoreImpl.store(keyId, created, value);

    String retrievedValue = inMemoryMetastoreImpl.load(keyId, created).get();

    assertEquals(value, retrievedValue, "Value loaded from persistence doesn't match what was stored.");
  }

  @Test
  void testLoadAndStoreWithInvalidKey() {
    String keyId = "ThisIsMyKey";
    Instant created = Instant.now();
    String value = "This is my value";

    inMemoryMetastoreImpl.store(keyId, created, value);

    assertFalse(inMemoryMetastoreImpl.load("some non-existent key", created).isPresent(),
        "Found value for non-existent key");
  }

  @Test
  void testLoadLatestMultipleCreatedAndValuesForKeyIdShouldReturnLatest() {
    String keyId = "ThisIsMyKey";
    Instant created = Instant.now();
    String value = "created value";
    inMemoryMetastoreImpl.store(keyId, created, value);

    Instant createdOneHourLater = created.plus(1, ChronoUnit.HOURS);
    String valueCreatedOneHourLater = value + createdOneHourLater;
    inMemoryMetastoreImpl.store(keyId, createdOneHourLater, valueCreatedOneHourLater);

    Instant createdOneDayLater = created.plus(1, ChronoUnit.DAYS);
    String valueCreatedOneDayLater = value + createdOneDayLater;
    inMemoryMetastoreImpl.store(keyId, createdOneDayLater, valueCreatedOneDayLater);

    Instant createdOneWeekEarlier = created.minus(7, ChronoUnit.DAYS);
    String valueCreatedOneWeekEarlier = value + createdOneWeekEarlier;
    inMemoryMetastoreImpl.store(keyId, createdOneWeekEarlier, valueCreatedOneWeekEarlier);

    assertEquals(valueCreatedOneDayLater, inMemoryMetastoreImpl.loadLatest(keyId).get());
  }

  @Test
  void testLoadLatestNonExistentKeyIdShouldReturnNull() {
    String keyId = "ThisIsMyKey";
    Instant created = Instant.now();
    String value = "created value";
    inMemoryMetastoreImpl.store(keyId, created, value);

    assertFalse(inMemoryMetastoreImpl.loadLatest("some non-existent key").isPresent());
  }

  @Test
  void testStoreWithDuplicateKeyShouldReturnFalse() {
    String keyId = "ThisIsMyKey";
    Instant created = Instant.now();
    String value = "this is the value";

    assertTrue(inMemoryMetastoreImpl.store(keyId, created, value));
    assertFalse(inMemoryMetastoreImpl.store(keyId, created, value));
  }
}
