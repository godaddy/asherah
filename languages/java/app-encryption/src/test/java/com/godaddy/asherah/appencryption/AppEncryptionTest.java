package com.godaddy.asherah.appencryption;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

import java.util.Optional;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.godaddy.asherah.appencryption.persistence.Persistence;

@ExtendWith(MockitoExtension.class)
class AppEncryptionTest {
  @Mock
  Persistence<String> persistence;
  @Mock
  AppEncryption<String, String> appEncryption;

  @Test
  void testLoadWithNonEmptyValue() {
    String dataRowRecord = "some data row record";
    when(persistence.load(anyString())).thenReturn(Optional.of(dataRowRecord));
    String expectedPayload = "some_payload";
    when(appEncryption.decrypt(anyString())).thenReturn(expectedPayload);
    when(appEncryption.load(anyString(), any())).thenCallRealMethod();

    String persistenceKey = "some_key";
    Optional<String> actualPayload = appEncryption.load(persistenceKey, persistence);
    assertTrue(actualPayload.isPresent());
    assertEquals(expectedPayload, actualPayload.get());
    verify(persistence).load(eq(persistenceKey));
    verify(appEncryption).decrypt(eq(dataRowRecord));
  }

  @Test
  void testLoadWithEmptyValue() {
    when(persistence.load(anyString())).thenReturn(Optional.empty());
    when(appEncryption.load(anyString(), any())).thenCallRealMethod();

    String persistenceKey = "key_with_no_value";
    Optional<String> actualPayload = appEncryption.load(persistenceKey, persistence);
    assertFalse(actualPayload.isPresent());
    verify(persistence).load(eq(persistenceKey));
    verify(appEncryption, never()).decrypt(any());
  }

  @Test
  void testStore() {
    String dataRowRecord = "some data row record";
    when(appEncryption.encrypt(anyString())).thenReturn(dataRowRecord);
    String expectedPersistenceKey = "some_key";
    when(persistence.store(anyString())).thenReturn(expectedPersistenceKey);
    when(appEncryption.store(anyString(), any())).thenCallRealMethod();
    
    String payload = "some_payload";
    String actualPersistenceKey = appEncryption.store(payload, persistence);
    assertEquals(expectedPersistenceKey, actualPersistenceKey);
    verify(appEncryption).encrypt(eq(payload));
    verify(persistence).store(eq(dataRowRecord));
  }

  @Test
  void testStoreWithPersistenceKey() {
    String dataRowRecord = "some data row record";
    String persistenceKey = "some_key";
    String payload = "some_payload";
    when(appEncryption.encrypt(payload)).thenReturn(dataRowRecord);
    doCallRealMethod().when(appEncryption).store(persistenceKey, payload, persistence);

    appEncryption.store(persistenceKey, payload, persistence);
    verify(appEncryption).encrypt(payload);
    verify(persistence).store(persistenceKey, dataRowRecord);
  }

}
