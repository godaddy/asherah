package com.godaddy.asherah.crypto.engine;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertEquals;

import java.nio.charset.StandardCharsets;
import java.security.SecureRandom;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

import com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAes256GcmCrypto;
import com.godaddy.asherah.crypto.engine.jdk.JdkAes256GcmCrypto;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;

/**
 * Tests to verify that BouncyCastle and JDK crypto implementations are interoperable.
 * Data encrypted with one implementation should be decryptable with the other.
 */
class CryptoEngineInteropTest {

  private AeadEnvelopeCrypto bouncyCrypto;
  private AeadEnvelopeCrypto jdkCrypto;
  private SecureRandom random;

  @BeforeEach
  void setUp() {
    bouncyCrypto = new BouncyAes256GcmCrypto();
    jdkCrypto = new JdkAes256GcmCrypto();
    random = new SecureRandom();
  }

  @Nested
  @DisplayName("BouncyCastle encrypts, JDK decrypts")
  class BouncyCastleToJdk {

    @ParameterizedTest
    @ValueSource(strings = {
        "TestString",
        "ᐊᓕᒍᖅ ᓂᕆᔭᕌᖓᒃᑯ ᓱᕋᙱᑦᑐᓐᓇᖅᑐᖓ ",
        "𠜎 𠜱 𠝹 𠱓 𠱸 𠲖 𠳏 𠳕 𠴕 𠵼 𠵿 𠸎"
    })
    void roundTripString(String testData) {
      // Use BouncyCastle to generate key (key format is independent of crypto engine)
      CryptoKey key = bouncyCrypto.generateKey();

      // Encrypt with BouncyCastle
      byte[] cipherText = bouncyCrypto.encrypt(testData.getBytes(StandardCharsets.UTF_8), key);

      // Decrypt with JDK
      String plainText = new String(jdkCrypto.decrypt(cipherText, key), StandardCharsets.UTF_8);

      assertEquals(testData, plainText);
    }

    @ParameterizedTest
    @ValueSource(ints = {1, 8, 16, 32, 64, 128, 1024, 4096})
    void roundTripRandomBytes(int testSize) {
      byte[] testData = new byte[testSize];
      random.nextBytes(testData);

      CryptoKey key = bouncyCrypto.generateKey();
      byte[] cipherText = bouncyCrypto.encrypt(testData, key);
      byte[] plainText = jdkCrypto.decrypt(cipherText, key);

      assertArrayEquals(testData, plainText);
    }
  }

  @Nested
  @DisplayName("JDK encrypts, BouncyCastle decrypts")
  class JdkToBouncyCastle {

    @ParameterizedTest
    @ValueSource(strings = {
        "TestString",
        "ᐊᓕᒍᖅ ᓂᕆᔭᕌᖓᒃᑯ ᓱᕋᙱᑦᑐᓐᓇᖅᑐᖓ ",
        "𠜎 𠜱 𠝹 𠱓 𠱸 𠲖 𠳏 𠳕 𠴕 𠵼 𠵿 𠸎"
    })
    void roundTripString(String testData) {
      // Use JDK to generate key
      CryptoKey key = jdkCrypto.generateKey();

      // Encrypt with JDK
      byte[] cipherText = jdkCrypto.encrypt(testData.getBytes(StandardCharsets.UTF_8), key);

      // Decrypt with BouncyCastle
      String plainText = new String(bouncyCrypto.decrypt(cipherText, key), StandardCharsets.UTF_8);

      assertEquals(testData, plainText);
    }

    @ParameterizedTest
    @ValueSource(ints = {1, 8, 16, 32, 64, 128, 1024, 4096})
    void roundTripRandomBytes(int testSize) {
      byte[] testData = new byte[testSize];
      random.nextBytes(testData);

      CryptoKey key = jdkCrypto.generateKey();
      byte[] cipherText = jdkCrypto.encrypt(testData, key);
      byte[] plainText = bouncyCrypto.decrypt(cipherText, key);

      assertArrayEquals(testData, plainText);
    }
  }

  @Nested
  @DisplayName("Key interoperability")
  class KeyInterop {

    @Test
    void keysGeneratedByBouncyCastleWorkWithJdk() {
      String testData = "test data for key interop";
      CryptoKey key = bouncyCrypto.generateKey();

      // Both engines should work with the same key
      byte[] cipherText1 = bouncyCrypto.encrypt(testData.getBytes(StandardCharsets.UTF_8), key);
      byte[] cipherText2 = jdkCrypto.encrypt(testData.getBytes(StandardCharsets.UTF_8), key);

      // Cross-decrypt
      String plainText1 = new String(jdkCrypto.decrypt(cipherText1, key), StandardCharsets.UTF_8);
      String plainText2 = new String(bouncyCrypto.decrypt(cipherText2, key), StandardCharsets.UTF_8);

      assertEquals(testData, plainText1);
      assertEquals(testData, plainText2);
    }

    @Test
    void keysGeneratedByJdkWorkWithBouncyCastle() {
      String testData = "test data for key interop";
      CryptoKey key = jdkCrypto.generateKey();

      // Both engines should work with the same key
      byte[] cipherText1 = jdkCrypto.encrypt(testData.getBytes(StandardCharsets.UTF_8), key);
      byte[] cipherText2 = bouncyCrypto.encrypt(testData.getBytes(StandardCharsets.UTF_8), key);

      // Cross-decrypt
      String plainText1 = new String(bouncyCrypto.decrypt(cipherText1, key), StandardCharsets.UTF_8);
      String plainText2 = new String(jdkCrypto.decrypt(cipherText2, key), StandardCharsets.UTF_8);

      assertEquals(testData, plainText1);
      assertEquals(testData, plainText2);
    }
  }

  @Nested
  @DisplayName("Envelope encryption interoperability")
  class EnvelopeInterop {

    @Test
    void envelopeEncryptWithBouncyCastleDecryptWithJdk() {
      byte[] testData = "envelope test data".getBytes(StandardCharsets.UTF_8);
      CryptoKey keyEncryptionKey = bouncyCrypto.generateKey();

      // Encrypt envelope with BouncyCastle
      var result = bouncyCrypto.envelopeEncrypt(testData, keyEncryptionKey);

      // Decrypt envelope with JDK
      byte[] plainText = jdkCrypto.envelopeDecrypt(
          result.getCipherText(),
          result.getEncryptedKey(),
          keyEncryptionKey.getCreated(),
          keyEncryptionKey);

      assertArrayEquals(testData, plainText);
    }

    @Test
    void envelopeEncryptWithJdkDecryptWithBouncyCastle() {
      byte[] testData = "envelope test data".getBytes(StandardCharsets.UTF_8);
      CryptoKey keyEncryptionKey = jdkCrypto.generateKey();

      // Encrypt envelope with JDK
      var result = jdkCrypto.envelopeEncrypt(testData, keyEncryptionKey);

      // Decrypt envelope with BouncyCastle
      byte[] plainText = bouncyCrypto.envelopeDecrypt(
          result.getCipherText(),
          result.getEncryptedKey(),
          keyEncryptionKey.getCreated(),
          keyEncryptionKey);

      assertArrayEquals(testData, plainText);
    }
  }
}

