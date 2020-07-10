package com.godaddy.asherah.appencryption.envelope;

import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.appencryption.utils.Json;

public class EnvelopeEncryptionBytesImpl implements EnvelopeEncryption<byte[]> {
  private static final Logger logger = LoggerFactory.getLogger(EnvelopeEncryptionBytesImpl.class);

  private final EnvelopeEncryption<JSONObject> envelopeEncryptionJson;

  /**
   * Creates a new {@code EnvelopeEncryptionBytesImpl} instance using the provided parameters. This is an
   * implementation of {@link EnvelopeEncryption} which uses byte[] as the Data Row Record format.
   *
   * @param envelopeEncryptionJson An {@link EnvelopeEncryption} object which uses {@link org.json.JSONObject} as the
   *                               Data Row Record format.
   */
  public EnvelopeEncryptionBytesImpl(final EnvelopeEncryption<JSONObject> envelopeEncryptionJson) {
    this.envelopeEncryptionJson = envelopeEncryptionJson;
  }

  @Override
  public byte[] decryptDataRowRecord(final byte[] dataRowRecord) {
    Json dataRowRecordJson = new Json(dataRowRecord);
    return envelopeEncryptionJson.decryptDataRowRecord(dataRowRecordJson.toJsonObject());
  }

  @Override
  public byte[] encryptPayload(final byte[] payload) {
    Json drrJson = new Json(envelopeEncryptionJson.encryptPayload(payload));
    return drrJson.toUtf8();
  }

  @Override
  public void close() {
    try {
      envelopeEncryptionJson.close();
    }
    catch (Exception e) {
      logger.error("unexpected exception during close", e);
    }
  }
}
