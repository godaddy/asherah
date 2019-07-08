package com.godaddy.asherah.appencryption.persistence;

import static org.junit.jupiter.api.Assertions.*;

import java.time.Instant;
import java.time.temporal.ChronoUnit;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

class MemoryPersistenceImplTest {
  MemoryPersistenceImpl<String> memoryPersistenceImpl;

  @BeforeEach
  void setUp() {
    memoryPersistenceImpl = new MemoryPersistenceImpl<>();
  }

  @Test
  void testLoadAndStoreWithValidKey() {
    String keyId = "ThisIsMyKey";
    Instant created = Instant.now();
    String value = "This is my value";

    memoryPersistenceImpl.store(keyId, created, value);

    String retrievedValue = memoryPersistenceImpl.load(keyId, created).get();

    assertEquals(value, retrievedValue, "Value loaded from persistence doesn't match what was stored.");
  }

  @Test
  void testLoadAndStoreWithInvalidKey() {
    String keyId = "ThisIsMyKey";
    Instant created = Instant.now();
    String value = "This is my value";

    memoryPersistenceImpl.store(keyId, created, value);

    assertFalse(memoryPersistenceImpl.load("some non-existent key", created).isPresent(),
        "Found value for non-existent key");
  }

  @Test
  void testLoadLatestValueMultipleCreatedAndValuesForKeyIdShouldReturnLatest() {
    String keyId = "ThisIsMyKey";
    Instant created = Instant.now();
    String value = "created value";
    memoryPersistenceImpl.store(keyId, created, value);

    Instant createdOneHourLater = created.plus(1, ChronoUnit.HOURS);
    String valueCreatedOneHourLater = value + createdOneHourLater;
    memoryPersistenceImpl.store(keyId, createdOneHourLater, valueCreatedOneHourLater);

    Instant createdOneDayLater = created.plus(1, ChronoUnit.DAYS);
    String valueCreatedOneDayLater = value + createdOneDayLater;
    memoryPersistenceImpl.store(keyId, createdOneDayLater, valueCreatedOneDayLater);

    Instant createdOneWeekEarlier = created.minus(7, ChronoUnit.DAYS);
    String valueCreatedOneWeekEarlier = value + createdOneWeekEarlier;
    memoryPersistenceImpl.store(keyId, createdOneWeekEarlier, valueCreatedOneWeekEarlier);

    assertEquals(valueCreatedOneDayLater, memoryPersistenceImpl.loadLatestValue(keyId).get());
  }

  @Test
  void testLoadLatestValueNonExistantKeyIdShouldReturnNull() {
    String keyId = "ThisIsMyKey";
    Instant created = Instant.now();
    String value = "created value";
    memoryPersistenceImpl.store(keyId, created, value);

    assertFalse(memoryPersistenceImpl.loadLatestValue("some non-existent key").isPresent());
  }

  @Test
  void testStoreWithDuplicateKeyShouldReturnFalse() {
    String keyId = "ThisIsMyKey";
    Instant created = Instant.now();
    String value = "this is the value";

    assertTrue(memoryPersistenceImpl.store(keyId, created, value));
    assertFalse(memoryPersistenceImpl.store(keyId, created, value));
  }
}
