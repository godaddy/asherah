package com.godaddy.asherah.appencryption.envelope;

import com.godaddy.asherah.appencryption.utils.SafeAutoCloseable;

/**
 * This defines the interface for interacting with the envelope encryption algorithm. It only interacts with bytes,
 * so it is up to the caller to determine how the bytes map to the first class object being used (e.g. JSON, String,
 * etc.).
 *
 * @param <D> The type that is being used as the Data Row Record format (e.g. JSON, Yaml, Protobuf, etc.).
 */
public interface EnvelopeEncryption<D> extends SafeAutoCloseable {
  /**
   * Uses an envelope encryption algorithm to decrypt a Data Row Record and return the payload.
   *
   * @param dataRowRecord The Data Row Record to decrypt.
   * @return A decrypted payload as bytes.
   */
  byte[] decryptDataRowRecord(D dataRowRecord);

  /**
   * Uses an envelope encryption algorithm to encrypt a payload and return the resulting Data Row Record.
   *
   * @param payload The payload to encrypt.
   * @return The Data Row Record that contains the now-encrypted payload and corresponding Data Row Key.
   */
  D encryptPayload(byte[] payload);
}
