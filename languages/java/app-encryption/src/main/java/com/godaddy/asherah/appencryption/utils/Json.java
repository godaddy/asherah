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

  public Json() {
    document = new JSONObject();
  }

  public Set<String> keySet() {
    return document.keySet();
  }

  public Json(final JSONObject jsonObject) {
    if (jsonObject == null) {
      throw new IllegalArgumentException("jsonObject is null!");
    }
    document = jsonObject;
  }

  public Json(final byte[] utf8Json) {
    document = convertUtf8ToJson(utf8Json);
  }

  public Json getJson(final String key) {
    return new Json(document.getJSONObject(key));
  }

  public Optional<Json> getOptionalJson(final String key) {
    return Optional.ofNullable(document.optJSONObject(key)).map(Json::new);
  }

  public String getString(final String key) {
    return document.getString(key);
  }

  public byte[] getBytes(final String key) {
    return Base64.getDecoder().decode(document.getString(key));
  }

  public Instant getInstant(final String key) {
    return Instant.ofEpochSecond(document.getLong(key));
  }

  public Optional<Boolean> getOptionalBoolean(final String key) {
    return Optional.ofNullable(document.optBoolean(key));
  }

  public JSONArray getJSONArray(final String key) {
    return document.getJSONArray(key);
  }

  public void put(final String key, final Instant instant) {
    document.put(key, instant.getEpochSecond());
  }

  public void put(final String key, final String string) {
    document.put(key, string);
  }

  public void put(final String key, final byte[] bytes) {
    document.put(key, Base64.getEncoder().encodeToString(bytes));
  }

  public void put(final String key, final boolean bool) {
    document.put(key, bool);
  }

  public void put(final String key, final JSONObject json) {
    document.put(key, json);
  }

  public void put(final String key, final Json json) {
    document.put(key, json.toJsonObject());
  }

  public void put(final String key, final List<?> jsonList) {
    document.put(key, jsonList);
  }

  public String toJsonString() {
    return document.toString();
  }

  public byte[] toUtf8() {
    return convertJsonToUtf8(document);
  }

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
