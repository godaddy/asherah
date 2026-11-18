package com.godaddy.asherah.securememory.ffmimpl;

import static org.junit.jupiter.api.Assertions.*;

import java.lang.reflect.Method;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.condition.EnabledForJreRange;
import org.junit.jupiter.api.condition.JRE;

import com.godaddy.asherah.securememory.Secret;
import com.godaddy.asherah.securememory.SecretFactory;

/**
 * Tests for FFM-based secret factory.
 * These tests only run on Java 22+ where FFM is available.
 */
@EnabledForJreRange(min = JRE.JAVA_22)
class FfmSecretFactoryTest {

  private SecretFactory ffmSecretFactory;
  private Class<?> ffmSecretClass;

  @BeforeEach
  void setUp() throws Exception {
    // Use reflection to create FFM factory since it's only available on Java 22+
    Class<?> factoryClass = Class.forName("com.godaddy.asherah.securememory.ffmimpl.FfmSecretFactory");
    ffmSecretFactory = (SecretFactory) factoryClass.getDeclaredConstructor().newInstance();
    ffmSecretClass = Class.forName("com.godaddy.asherah.securememory.ffmimpl.FfmProtectedMemorySecret");
  }

  @Test
  void testCreateSecretByteArrayReturnsFfmSecret() {
    Secret secret = ffmSecretFactory.createSecret(new byte[] {0, 1, 2});
    try {
      assertTrue(ffmSecretClass.isInstance(secret),
          "Expected FfmProtectedMemorySecret but got " + secret.getClass().getName());
    }
    finally {
      secret.close();
    }
  }

  @Test
  void testCreateSecretCharArrayReturnsFfmSecret() {
    Secret secret = ffmSecretFactory.createSecret(new char[] {'a', 'b', 'c'});
    try {
      assertTrue(ffmSecretClass.isInstance(secret),
          "Expected FfmProtectedMemorySecret but got " + secret.getClass().getName());
    }
    finally {
      secret.close();
    }
  }

  @Test
  void testIsAvailableReturnsTrueOnJava22Plus() throws Exception {
    // Get the static isAvailable method
    Class<?> factoryClass = Class.forName("com.godaddy.asherah.securememory.ffmimpl.FfmSecretFactory");
    Method isAvailableMethod = factoryClass.getMethod("isAvailable");

    boolean isAvailable = (boolean) isAvailableMethod.invoke(null);
    assertTrue(isAvailable, "FFM should be available on Java 22+");
  }

  @Test
  void testFactoryDetectsPlatform() {
    // Simply creating the factory without exception proves platform detection works
    assertNotNull(ffmSecretFactory, "Factory should be created successfully");
  }

  @Test
  void testCreatedSecretContainsCorrectData() {
    byte[] originalData = new byte[] {10, 20, 30, 40, 50};
    Secret secret = ffmSecretFactory.createSecret(originalData.clone());
    try {
      secret.withSecretBytes(decryptedBytes -> {
        assertArrayEquals(originalData, decryptedBytes);
        return null;
      });
    }
    finally {
      secret.close();
    }
  }

  @Test
  void testCreatedSecretFromCharsContainsCorrectData() {
    char[] originalChars = new char[] {'h', 'e', 'l', 'l', 'o'};
    Secret secret = ffmSecretFactory.createSecret(originalChars.clone());
    try {
      secret.withSecretUtf8Chars(decryptedChars -> {
        assertArrayEquals(originalChars, decryptedChars);
        return null;
      });
    }
    finally {
      secret.close();
    }
  }
}
