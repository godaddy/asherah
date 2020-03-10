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
class SessionTest {
  @Mock
  Persistence<String> persistence;
  @Mock
  Session<String, String> session;

  @Test
  void testLoadWithNonEmptyValue() {
    String dataRowRecord = "some data row record";
    when(persistence.load(anyString())).thenReturn(Optional.of(dataRowRecord));
    String expectedPayload = "some_payload";
    when(session.decrypt(anyString())).thenReturn(expectedPayload);
    when(session.load(anyString(), any())).thenCallRealMethod();

    String persistenceKey = "some_key";
    Optional<String> actualPayload = session.load(persistenceKey, persistence);
    assertTrue(actualPayload.isPresent());
    assertEquals(expectedPayload, actualPayload.get());
    verify(persistence).load(eq(persistenceKey));
    verify(session).decrypt(eq(dataRowRecord));
  }

  @Test
  void testLoadWithEmptyValue() {
    when(persistence.load(anyString())).thenReturn(Optional.empty());
    when(session.load(anyString(), any())).thenCallRealMethod();

    String persistenceKey = "key_with_no_value";
    Optional<String> actualPayload = session.load(persistenceKey, persistence);
    assertFalse(actualPayload.isPresent());
    verify(persistence).load(eq(persistenceKey));
    verify(session, never()).decrypt(any());
  }

  @Test
  void testStore() {
    String dataRowRecord = "some data row record";
    when(session.encrypt(anyString())).thenReturn(dataRowRecord);
    String expectedPersistenceKey = "some_key";
    when(persistence.store(anyString())).thenReturn(expectedPersistenceKey);
    when(session.store(anyString(), any())).thenCallRealMethod();

    String payload = "some_payload";
    String actualPersistenceKey = session.store(payload, persistence);
    assertEquals(expectedPersistenceKey, actualPersistenceKey);
    verify(session).encrypt(eq(payload));
    verify(persistence).store(eq(dataRowRecord));
  }

  @Test
  void testStoreWithPersistenceKey() {
    String dataRowRecord = "some data row record";
    String persistenceKey = "some_key";
    String payload = "some_payload";
    when(session.encrypt(payload)).thenReturn(dataRowRecord);
    doCallRealMethod().when(session).store(persistenceKey, payload, persistence);

    session.store(persistenceKey, payload, persistence);
    verify(session).encrypt(payload);
    verify(persistence).store(persistenceKey, dataRowRecord);
  }

}
