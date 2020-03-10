package com.godaddy.asherah.crypto.engine;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertEquals;

import java.nio.charset.StandardCharsets;

import com.godaddy.asherah.crypto.AeadCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;

public class CryptoInterop {

  void interopStringTest(String testString, AeadCrypto crypto1, AeadCrypto crypto2) {
    CryptoKey key1 = crypto1.generateKey();
    byte[] cipherText1 = crypto1.encrypt(testString.getBytes(StandardCharsets.UTF_8), key1);
    String plainText1 = new String(crypto2.decrypt(cipherText1, key1), StandardCharsets.UTF_8);
    assertEquals(plainText1, testString);

    CryptoKey key2 = crypto2.generateKey();
    byte[] cipherText2 = crypto2.encrypt(testString.getBytes(StandardCharsets.UTF_8), key2);
    String plainText2 = new String(crypto1.decrypt(cipherText2, key2), StandardCharsets.UTF_8);
    assertEquals(plainText2, testString);
  }

  void interopTest(byte[] testData, AeadCrypto crypto1, AeadCrypto crypto2) {
    CryptoKey key1 = crypto1.generateKey();
    byte[] plainText1 = crypto2.decrypt(crypto1.encrypt(testData, key1), key1);
    assertArrayEquals(plainText1, testData);

    CryptoKey key2 = crypto2.generateKey();
    byte[] plainText2 = crypto1.decrypt(crypto2.encrypt(testData, key2), key2);
    assertArrayEquals(plainText2, testData);
  }

  /*
  * TODO: JCA engine needs some work -- cipher isn't initialized so none of the JCA or interop tests work.
  @ParameterizedTest
  @ValueSource(strings = { "TestString", "ᐊᓕᒍᖅ ᓂᕆᔭᕌᖓᒃᑯ ᓱᕋᙱᑦᑐᓐᓇᖅᑐᖓ ", "𠜎 𠜱 𠝹 𠱓 𠱸 𠲖 𠳏 𠳕 𠴕 𠵼 𠵿 𠸎 𠸏 𠹷 𠺝 𠺢 𠻗 𠻹 𠻺 𠼭 𠼮 𠽌 𠾴 𠾼 𠿪 𡁜 𡁯 𡁵 𡁶 𡁻 𡃁 𡃉 𡇙 𢃇 𢞵 𢫕 𢭃 𢯊 𢱑 𢱕 𢳂 𢴈 𢵌 𢵧 𢺳 𣲷 𤓓 𤶸 𤷪 𥄫 𦉘 𦟌 𦧲 𦧺 𧨾 𨅝 𨈇 𨋢 𨳊 𨳍 𨳒 𩶘" })
  void roundTripString(String testData) {
    AeadCrypto c2 = new JcaAes256GcmCrypto();
    CryptoKey ck = c2.generateKey();

    byte[] ct = c2.encrypt(testData.getBytes(StandardCharsets.UTF_8), ck);

    interopStringTest(testData, new BouncyAes256GcmCrypto(), new JcaAes256GcmCrypto());
  }

  @ParameterizedTest
  @ValueSource(ints = { 1, 8, 16, 32, 64, 128 })
  void roundTripRandom(int testSize) {
    byte[] testData = new byte[testSize];
    interopTest(testData, new BouncyAes256GcmCrypto(), new JcaAes256GcmCrypto());
  }
  */
}


