package com.godaddy.asherah.appencryption;

import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryption;
import com.godaddy.asherah.appencryption.utils.Json;

public class SessionJsonImpl<D> implements Session<JSONObject, D> {
  private static final Logger logger = LoggerFactory.getLogger(SessionJsonImpl.class);

  private final EnvelopeEncryption<D> envelopeEncryption;

  /**
   * Constructor for SessionJsonImpl.
   * @param envelopeEncryption a {@link EnvelopeEncryption} object.
   */
  public SessionJsonImpl(final EnvelopeEncryption<D> envelopeEncryption) {
    this.envelopeEncryption = envelopeEncryption;
  }

  @Override
  public JSONObject decrypt(final D dataRowRecord) {
    byte[] jsonAsUtf8Bytes = envelopeEncryption.decryptDataRowRecord(dataRowRecord);

    return new Json(jsonAsUtf8Bytes).toJsonObject();
  }

  @Override
  public D encrypt(final JSONObject payload) {
    byte[] jsonAsUtf8Bytes = new Json(payload).toUtf8();

    return envelopeEncryption.encryptPayload(jsonAsUtf8Bytes);
  }

  @Override
  public void close() {
    try {
      envelopeEncryption.close();
    }
    catch (Exception e) {
      logger.error("unexpected exception during close", e);
    }
  }
}

