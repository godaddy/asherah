package com.godaddy.asherah.appencryption.persistence;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

import java.util.UUID;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

@ExtendWith(MockitoExtension.class)
class PersistenceTest {
  @Mock
  Persistence<String> persistence;

  @Test
  void testStore() {
    String expectedPersistenceKey = "some_key";
    when(persistence.generateKey(any())).thenReturn(expectedPersistenceKey);
    doNothing().when(persistence).store(anyString(), anyString());
    doCallRealMethod().when(persistence).store(anyString());

    String value = "some_value";
    String actualPersistenceKey = persistence.store(value);
    assertEquals(expectedPersistenceKey, actualPersistenceKey);
    verify(persistence).generateKey(value);
    verify(persistence).store(eq(expectedPersistenceKey), eq(value));
  }

  @Test
  void testGenerateKey() {
    String value= "unused_value";
    when(persistence.generateKey(anyString())).thenCallRealMethod();

    String returnedValue = persistence.generateKey(value);
    // Just verify it's a valid UUID (note JDK8 has known bug https://bugs.java.com/view_bug.do?bug_id=8159339)
    UUID.fromString(returnedValue);
  }

}
