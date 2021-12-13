package com.godaddy.asherah.crypto.engine;

import static java.time.temporal.ChronoUnit.MINUTES;
import static org.junit.jupiter.api.Assertions.*;

import com.godaddy.asherah.appencryption.exceptions.AppEncryptionException;
import com.godaddy.asherah.appencryption.testhelpers.ByteArray;
import com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAes256GcmCrypto;
import com.godaddy.asherah.crypto.engine.bouncycastle.BouncyChaCha20Poly1305Crypto;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;

import java.io.BufferedReader;
import java.io.ByteArrayInputStream;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.io.Reader;
import java.nio.charset.Charset;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.security.SecureRandom;
import java.time.Instant;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

public abstract class GenericAeadCryptoTest {
  private AeadEnvelopeCrypto crypto;
  private SecureRandom random;

  protected abstract AeadEnvelopeCrypto getCryptoInstance();

  @BeforeEach
  void setUp() {
    crypto = getCryptoInstance();
    random = new SecureRandom();
  }

  @Test
  void generateKey() {
    CryptoKey key = crypto.generateKey();
    assertNotNull(key);
    Instant created = key.getCreated();

    //Ensure that the create date of the key is somewhat valid
    assertFalse(created.isBefore(Instant.now().minus(1, MINUTES)),
        "Created date is more than 1 minute in the past");
    assertFalse(created.isAfter(Instant.now().plus(1, MINUTES)),
        "Create date is 1 minute in the future");

    assertFalse(key.withKey(ByteArray::isAllZeros), "Key is all zeros!");
  }

  @ParameterizedTest
  @ValueSource(strings = {
      "TestString",
      "ᐊᓕᒍᖅ ᓂᕆᔭᕌᖓᒃᑯ ᓱᕋᙱᑦᑐᓐᓇᖅᑐᖓ ",
      "𠜎 𠜱 𠝹 𠱓 𠱸 𠲖 𠳏 𠳕 𠴕 𠵼 𠵿 𠸎 𠸏 𠹷 𠺝 𠺢 𠻗 𠻹 𠻺 𠼭 𠼮 𠽌 𠾴 𠾼 𠿪 𡁜 𡁯 𡁵 𡁶 𡁻 𡃁 𡃉 𡇙 𢃇 𢞵 𢫕 𢭃 𢯊 𢱑 𢱕 𢳂 𢴈 𢵌 𢵧 𢺳 𣲷 𤓓 𤶸 𤷪 𥄫 𦉘 𦟌 𦧲 𦧺 𧨾 𨅝 𨈇 𨋢 𨳊 𨳍 𨳒 𩶘" })
  void roundTripString(String testData) {
    CryptoKey key = crypto.generateKey();
    byte[] cipherText = crypto.encrypt(testData.getBytes(StandardCharsets.UTF_8), key);
    String plainText = new String(crypto.decrypt(cipherText, key), StandardCharsets.UTF_8);
    assertEquals(plainText, testData);
  }

  @ParameterizedTest
  @ValueSource(ints = { 8 })
  void roundTripRandom(int testSize) {
    byte[] testData = new byte[testSize];
    random.nextBytes(testData);

    CryptoKey key = crypto.generateKey();
    byte[] cipherText = crypto.encrypt(testData, key);
    byte[] plainText = crypto.decrypt(cipherText, key);
    assertArrayEquals(plainText, testData);
  }

  @Test
  void testRoundTripStringWithWrongKeyDecryptShouldFail() {
    String testData = "blahblah";
    CryptoKey rightKey = crypto.generateKey();
    byte[] cipherText = crypto.encrypt(testData.getBytes(StandardCharsets.UTF_8), rightKey);
    CryptoKey wrongKey = crypto.generateKey();
    assertThrows(AppEncryptionException.class, () -> crypto.decrypt(cipherText, wrongKey));
  }


  @ParameterizedTest
  @ValueSource(strings = {
    "TestString",
    "ᐊᓕᒍᖅ ᓂᕆᔭᕌᖓᒃᑯ ᓱᕋᙱᑦᑐᓐᓇᖅᑐᖓ ",
    "𠜎 𠜱 𠝹 𠱓 𠱸 𠲖 𠳏 𠳕 𠴕 𠵼 𠵿 𠸎 𠸏 𠹷 𠺝 𠺢 𠻗 𠻹 𠻺 𠼭 𠼮 𠽌 𠾴 𠾼 𠿪 𡁜 𡁯 𡁵 𡁶 𡁻 𡃁 𡃉 𡇙 𢃇 𢞵 𢫕 𢭃 𢯊 𢱑 𢱕 𢳂 𢴈 𢵌 𢵧 𢺳 𣲷 𤓓 𤶸 𤷪 𥄫 𦉘 𦟌 𦧲 𦧺 𧨾 𨅝 𨈇 𨋢 𨳊 𨳍 𨳒 𩶘" })
  void roundTripStream(String testData) throws IOException {
    // TODO Don't use static values. Create temp file, populate, enc, dec. Add a sanity check that tempfile> buffer size
    this.crypto = new BouncyAes256GcmCrypto();
    CryptoKey rightKey = crypto.generateKey();
    InputStream inputStream = new ByteArrayInputStream(testData.getBytes());
    OutputStream outputStream = new FileOutputStream(testData+"_encrypt.txt");
    // Encrypt stream
    crypto.encryptStream(inputStream, outputStream, rightKey);

    inputStream = new FileInputStream(testData+"_encrypt.txt");
    outputStream = new FileOutputStream(testData+"_decrypt.txt");
    // Decrypt stream
    crypto.decryptStream(inputStream,outputStream, rightKey);

    // Read value from the file written by the OutputStream & compare
    StringBuilder textBuilder = new StringBuilder();
    inputStream = new FileInputStream(testData+"_decrypt.txt");

    try (Reader reader = new BufferedReader(new InputStreamReader
      (inputStream, Charset.forName(StandardCharsets.UTF_8.name())))) {
      int data;
      while ((data = reader.read()) != -1) {
        textBuilder.append((char) data);
      }
    }
    assertEquals(testData, textBuilder.toString());
    Files.deleteIfExists(Paths.get(testData+"_decrypt.txt"));
    Files.deleteIfExists(Paths.get(testData+"_encrypt.txt"));
  }
}
