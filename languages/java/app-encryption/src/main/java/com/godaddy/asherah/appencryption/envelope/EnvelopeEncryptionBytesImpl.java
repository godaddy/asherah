package com.godaddy.asherah.appencryption.envelope;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.appencryption.utils.Json;

public class EnvelopeEncryptionBytesImpl implements EnvelopeEncryption<byte[]> {
  private static final Logger logger = LoggerFactory.getLogger(EnvelopeEncryptionBytesImpl.class);

  private final EnvelopeEncryptionJsonImpl envelopeEncryptionJsonImpl;

  public EnvelopeEncryptionBytesImpl(final EnvelopeEncryptionJsonImpl envelopeEncryptionJsonImpl) {
    this.envelopeEncryptionJsonImpl = envelopeEncryptionJsonImpl;
  }

  @Override
  public byte[] decryptDataRowRecord(final byte[] dataRowRecord) {
    Json dataRowRecordJson = new Json(dataRowRecord);
    return envelopeEncryptionJsonImpl.decryptDataRowRecord(dataRowRecordJson.toJsonObject());
  }

  @Override
  public byte[] encryptPayload(final byte[] payload) {
    Json drrJson = new Json(envelopeEncryptionJsonImpl.encryptPayload(payload));
    return drrJson.toUtf8();
  }

  @Override
  public void close() {
    try {
      envelopeEncryptionJsonImpl.close();
    }
    catch (Exception e) {
      logger.error("unexpected exception during close", e);
    }
  }

}
