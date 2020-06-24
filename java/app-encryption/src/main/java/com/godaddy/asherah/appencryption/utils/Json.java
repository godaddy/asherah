package com.godaddy.asherah.appencryption.utils;

import java.nio.ByteBuffer;
import java.nio.CharBuffer;
import java.nio.charset.Charset;
import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.util.Base64;
import java.util.List;
import java.util.Optional;
import java.util.Set;

import org.json.JSONArray;
import org.json.JSONObject;

public class Json {
  private static final Charset Utf8Charset = StandardCharsets.UTF_8;

  private final JSONObject document;

  /**
   * Constructor for Json.
   */
  public Json() {
    document = new JSONObject();
  }

  /**
   * Convert the {@link org.json.JSONObject} to a {@link java.util.Set} object.
   * @return the json document as a {@link java.util.Set} object.
   */
  public Set<String> keySet() {
    return document.keySet();
  }

  /**
   * Constructor for Json.
   * @param jsonObject a {@link org.json.JSONObject} object.
   */
  public Json(final JSONObject jsonObject) {
    if (jsonObject == null) {
      throw new IllegalArgumentException("jsonObject is null!");
    }
    document = jsonObject;
  }

  /**
   * Constructor for Json.
   * @param utf8Json an array of {@link byte} objects.
   */
  public Json(final byte[] utf8Json) {
    document = convertUtf8ToJson(utf8Json);
  }

  /**
   * Gets the Json associated with a given key.
   * @param key The key whose value needs to be retrieved.
   * @return The value associated with the key, as a string.
   */
  public Json getJson(final String key) {
    return new Json(document.getJSONObject(key));
  }

  /**
   * Gets the Json associated with a given key.
   * @param key The key whose value needs to be retrieved.
   * @return An <code>Optional&lt;Json&gt;</code> if it exists, else null
   */
  public Optional<Json> getOptionalJson(final String key) {
    return Optional.ofNullable(document.optJSONObject(key)).map(Json::new);
  }

  /**
   * Gets the string value associated with a given key.
   * @param key The key whose value needs to be retrieved.
   * @return The value associated with the key, as a string.
   */
  public String getString(final String key) {
    return document.getString(key);
  }

  /**
   * Gets the bytes value associated with a given key.
   * @param key The key whose value needs to be retrieved.
   * @return The value associated with the key, as a bytes array.
   */
  public byte[] getBytes(final String key) {
    return Base64.getDecoder().decode(document.getString(key));
  }

  /**
   * Gets the long value associated with a given key.
   * @param key The key whose value needs to be retrieved.
   * @return The value time associated with the key.
   */
  public Instant getInstant(final String key) {
    return Instant.ofEpochSecond(document.getLong(key));
  }

  /**
   * Gets the boolean value associated with a given key.
   * @param key The key whose value needs to be retrieved.
   * @return An <code>Optional&lt;Boolean&gt;</code> if it exists, else null.
   */
  public Optional<Boolean> getOptionalBoolean(final String key) {
    return Optional.ofNullable(document.optBoolean(key));
  }

  /**
   * Gets the JSONArray associated with a given key.
   * @param key The key whose value needs to be retrieved.
   * @return The value time associated with the key.
   */
  public JSONArray getJSONArray(final String key) {
    return document.getJSONArray(key);
  }

  /**
   * Adds a new entry to the {@link Json} object where the value is an {@link java.time.Instant} object.
   * @param key The Key.
   * @param instant The value associated with the key.
   */
  public void put(final String key, final Instant instant) {
    document.put(key, instant.getEpochSecond());
  }

  /**
   * Adds a new entry to the {@link Json} object where the value is an {@link java.lang.String}.
   * @param key The Key.
   * @param string The value associated with the key.
   */
  public void put(final String key, final String string) {
    document.put(key, string);
  }

  /**
   * Adds a new entry to the {@link Json} object where the value is a {@link byte} array.
   * @param key The Key.
   * @param bytes The value associated with the key.
   */
  public void put(final String key, final byte[] bytes) {
    document.put(key, Base64.getEncoder().encodeToString(bytes));
  }

  /**
   * Adds a new entry to the {@link Json} object where the value is a boolean.
   * @param key The Key.
   * @param bool The value associated with the key.
   */
  public void put(final String key, final boolean bool) {
    document.put(key, bool);
  }

  /**
   * Adds a new entry to the {@link Json} object where the value is an {@link org.json.JSONObject} object.
   * @param key The Key.
   * @param json The value associated with the key.
   */
  public void put(final String key, final JSONObject json) {
    document.put(key, json);
  }

  /**
   * Adds a new entry to the {@link Json} object where the value is an {@link Json} object.
   * @param key The Key.
   * @param json The value associated with the key.
   */
  public void put(final String key, final Json json) {
    document.put(key, json.toJsonObject());
  }

  /**
   * Adds a new entry to the {@link Json} object where the value is an {@link java.util.List}.
   * @param key The Key.
   * @param jsonList The value associated with the key.
   */
  public void put(final String key, final List<?> jsonList) {
    document.put(key, jsonList);
  }

  /**
   * Converts the {@link Json} to a string.
   * @return The json document as a string.
   */
  public String toJsonString() {
    return document.toString();
  }

  /**
   * Converts the {@link Json} to a bytes array.
   * @return The json document as a bytes array.
   */
  public byte[] toUtf8() {
    return convertJsonToUtf8(document);
  }

  /**
   * Converts the {@link Json} to a {@link org.json.JSONObject} object.
   * @return The json document a {@link org.json.JSONObject} object.
   */
  public JSONObject toJsonObject() {
    return document;
  }

  private static byte[] convertJsonToUtf8(final JSONObject jsonObject) {
    ByteBuffer bb = Utf8Charset.encode(jsonObject.toString());
    byte[] plainText = new byte[bb.remaining()];
    bb.get(plainText);
    return plainText;
  }

  private JSONObject convertUtf8ToJson(final byte[] utf8) {
    ByteBuffer bb = ByteBuffer.wrap(utf8);
    CharBuffer cb = Utf8Charset.decode(bb);
    return new JSONObject(cb.toString());
  }
}
