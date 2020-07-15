package com.godaddy.asherah.utils;

import java.nio.charset.StandardCharsets;

import org.apache.commons.text.RandomStringGenerator;
import org.json.JSONObject;

public final class PayloadGenerator {
  private static final RandomStringGenerator RANDOM_STRING_GENERATOR = new RandomStringGenerator.Builder()
    .withinRange('0', 'z')
    .filteredBy(Character::isLetterOrDigit)
    .build();
  private static final int DEFAULT_BYTE_SIZE = 20;

  private PayloadGenerator() {
  }

  public static byte[] createDefaultRandomBytePayload() {
    return createRandomBytePayload(DEFAULT_BYTE_SIZE);
  }

  public static byte[] createRandomBytePayload(final int size) {
    return RANDOM_STRING_GENERATOR.generate(size).getBytes(StandardCharsets.UTF_8);
  }

  public static JSONObject createDefaultRandomJsonPayload() {
    return createRandomJsonPayload(DEFAULT_BYTE_SIZE);
  }

  public static JSONObject createRandomJsonPayload(final int size) {
    // This will end up having an extra 10 bytes from json overhead + key, meh
    JSONObject json = new JSONObject();
    json.put("key", RANDOM_STRING_GENERATOR.generate(size));
    return json;
  }
}
