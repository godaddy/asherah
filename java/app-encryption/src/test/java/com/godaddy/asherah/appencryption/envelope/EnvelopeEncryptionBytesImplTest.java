package com.godaddy.asherah.appencryption.envelope;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

import org.json.JSONObject;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.godaddy.asherah.appencryption.utils.Json;
import com.google.common.collect.ImmutableMap;

@ExtendWith(MockitoExtension.class)
class EnvelopeEncryptionBytesImplTest {
  @Mock
  EnvelopeEncryptionJsonImpl envelopeEncryptionJsonImpl;

  @InjectMocks
  EnvelopeEncryptionBytesImpl envelopeEncryptionBytesImpl;

  @Test
  void testDecryptDataRowRecord() {
    byte[] expectedBytes = new byte[]{0, 1};
    // JSONObject doesn't play nice with Mockito, but we really just care about the base64 and utf8 operations succeeding
    when(envelopeEncryptionJsonImpl.decryptDataRowRecord(any())).thenReturn(expectedBytes);

    byte[] dataRowRecordBytes = new Json(new JSONObject(ImmutableMap.of("key", "value"))).toUtf8();
    byte[] actualBytes = envelopeEncryptionBytesImpl.decryptDataRowRecord(dataRowRecordBytes);
    assertArrayEquals(expectedBytes, actualBytes);
  }

  @Test
  void testEncryptPayload() {
    JSONObject dataRowRecord = new JSONObject(ImmutableMap.of("key", "value"));
    byte[] expectedBytes = new byte[]{123, 34, 107, 101, 121, 34, 58, 34, 118, 97, 108, 117, 101, 34, 125};
    when(envelopeEncryptionJsonImpl.encryptPayload(any())).thenReturn(dataRowRecord);

    byte[] actualResult = envelopeEncryptionBytesImpl.encryptPayload(new byte[]{0, 1});
    assertArrayEquals(expectedBytes, actualResult);
  }

  @Test
  void testCloseSuccess() {
    envelopeEncryptionBytesImpl.close();

    // Verify proper resources are closed
    verify(envelopeEncryptionJsonImpl).close();
  }

  @Test
  void testCloseWithCloseFailShouldReturn() {
    doThrow(RuntimeException.class).when(envelopeEncryptionJsonImpl).close();
    envelopeEncryptionBytesImpl.close();

    verify(envelopeEncryptionJsonImpl).close();
  }

}
