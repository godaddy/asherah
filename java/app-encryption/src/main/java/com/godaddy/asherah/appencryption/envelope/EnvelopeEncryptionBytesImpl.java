package com.godaddy.asherah.appencryption.envelope;

import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.appencryption.utils.Json;

public class EnvelopeEncryptionBytesImpl implements EnvelopeEncryption<byte[]> {
  private static final Logger logger = LoggerFactory.getLogger(EnvelopeEncryptionBytesImpl.class);

  private final EnvelopeEncryption<JSONObject> envelopeEncryptionJson;

  /**
   * Constructor for EnvelopeEncryptionBytesImpl.
   * @param envelopeEncryptionJson a {@link com.godaddy.asherah.appencryption.envelope.EnvelopeEncryption} object.
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
