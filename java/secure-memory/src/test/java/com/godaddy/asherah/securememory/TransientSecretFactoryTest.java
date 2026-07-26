package com.godaddy.asherah.securememory;

import static org.junit.jupiter.api.Assertions.*;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.condition.EnabledForJreRange;
import org.junit.jupiter.api.condition.JRE;

class TransientSecretFactoryTest {
  TransientSecretFactory transientSecretFactory;

  @BeforeEach
  void setUp() {
    // Reset to default behavior before each test
    TransientSecretFactory.setPreferJna(false);
    transientSecretFactory = new TransientSecretFactory();
  }

  @AfterEach
  void tearDown() {
    // Reset preference after tests
    TransientSecretFactory.setPreferJna(false);
  }

  @Test
  void testCreateSecretByteArray() {
    Secret secret = transientSecretFactory.createSecret(new byte[] {0, 1});
    assertNotNull(secret);
    // Verify the secret works correctly
    secret.withSecretBytes(bytes -> {
      assertEquals(2, bytes.length);
      return null;
    });
    if (secret instanceof AutoCloseable) {
      try {
        ((AutoCloseable) secret).close();
      }
      catch (Exception e) {
        // Ignore
      }
    }
  }

  @Test
  void testCreateSecretCharArray() {
    Secret secret = transientSecretFactory.createSecret(new char[] {'a', 'b'});
    assertNotNull(secret);
    // Verify the secret works correctly
    secret.withSecretUtf8Chars(chars -> {
      assertEquals(2, chars.length);
      return null;
    });
    if (secret instanceof AutoCloseable) {
      try {
        ((AutoCloseable) secret).close();
      }
      catch (Exception e) {
        // Ignore
      }
    }
  }

  @Test
  @EnabledForJreRange(min = JRE.JAVA_22)
  void testAutoDetectsFfmOnJava22Plus() {
    // On Java 22+, FFM should be auto-detected and used
    TransientSecretFactory factory = new TransientSecretFactory();
    assertTrue(factory.isUsingFfm(), "Should auto-detect and use FFM on Java 22+");
  }

  @Test
  @EnabledForJreRange(min = JRE.JAVA_22)
  void testPreferJnaForcesJnaOnJava22Plus() {
    // When preferJna is set, JNA should be used even on Java 22+
    TransientSecretFactory.setPreferJna(true);
    TransientSecretFactory factory = new TransientSecretFactory();
    assertFalse(factory.isUsingFfm(), "Should use JNA when preferJna is set");
  }

  @Test
  @EnabledForJreRange(max = JRE.JAVA_21)
  void testUsesJnaOnOlderJava() {
    // On Java 21 and below, JNA should be used
    TransientSecretFactory factory = new TransientSecretFactory();
    assertFalse(factory.isUsingFfm(), "Should use JNA on Java < 22");
  }
}
