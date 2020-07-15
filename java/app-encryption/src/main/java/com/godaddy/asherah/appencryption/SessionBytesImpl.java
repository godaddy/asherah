package com.godaddy.asherah.appencryption;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryption;

public class SessionBytesImpl<D> implements Session<byte[], D> {
  private static final Logger logger = LoggerFactory.getLogger(SessionBytesImpl.class);

  private final EnvelopeEncryption<D> envelopeEncryption;

  /**
   * Creates a new {@code SessionBytesImpl} instance using the provided {@link EnvelopeEncryption} object. An
   * implementation of {@link Session} that encrypts a payload of type byte[].
   *
   * @param envelopeEncryption An implementation of {@link EnvelopeEncryption} that uses byte[] as the Data Row
   *                           Record format.
   */
  public SessionBytesImpl(final EnvelopeEncryption<D> envelopeEncryption) {
    this.envelopeEncryption = envelopeEncryption;
  }

  @Override
  public byte[] decrypt(final D dataRowRecord) {
    return envelopeEncryption.decryptDataRowRecord(dataRowRecord);
  }

  @Override
  public D encrypt(final byte[] payload) {
    return envelopeEncryption.encryptPayload(payload);
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

