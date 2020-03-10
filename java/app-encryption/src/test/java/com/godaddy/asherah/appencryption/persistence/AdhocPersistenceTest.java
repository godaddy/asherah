package com.godaddy.asherah.appencryption.persistence;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

import java.util.Optional;
import java.util.function.BiConsumer;
import java.util.function.Function;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

@ExtendWith(MockitoExtension.class)
class AdhocPersistenceTest {
  @Mock
  Function<String, Optional<String>> persistenceLoad;
  @Mock
  BiConsumer<String, String> persistenceStore;
  @InjectMocks
  AdhocPersistence<String> adhocPersistence;

  @Test
  void testConstructor() {
    Persistence<String> adhocPersistence = new AdhocPersistence<>(persistenceLoad, persistenceStore);
    assertNotNull(adhocPersistence);
  }

  @Test
  void testLoad() {
    String expectedValue = "some_value";
    when(persistenceLoad.apply(anyString())).thenReturn(Optional.of(expectedValue));
    
    String key = "some_key";
    Optional<String> actualValue = adhocPersistence.load(key);
    assertTrue(actualValue.isPresent());
    assertEquals(expectedValue, actualValue.get());
    verify(persistenceLoad).apply(key);
  }

  @Test
  void testStore() {
    String key = "some_key";
    String value = "some_value";
    adhocPersistence.store(key, value);
    verify(persistenceStore).accept(key, value);
  }

}
