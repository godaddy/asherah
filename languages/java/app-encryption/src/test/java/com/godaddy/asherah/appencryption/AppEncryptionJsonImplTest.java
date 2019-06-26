package com.godaddy.asherah.appencryption;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

import org.json.JSONObject;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryption;
import com.godaddy.asherah.appencryption.utils.Json;

@ExtendWith(MockitoExtension.class)
class AppEncryptionJsonImplTest {

  @Mock
  EnvelopeEncryption<String> envelopeEncryption;
  @InjectMocks
  AppEncryptionJsonImpl<String> appEncryptionJsonImpl;
  
  @Test
  void testConstructor() {
    AppEncryption<?, ?> appEncryption = new AppEncryptionJsonImpl<>(envelopeEncryption);
    assertNotNull(appEncryption);
  }

  @Test
  void testDecrypt() {
    JSONObject expectedJson = new JSONObject("{\"some_key\": 123}");
    byte[] utf8Bytes = new Json(expectedJson).toUtf8();
    when(envelopeEncryption.decryptDataRowRecord(any())).thenReturn(utf8Bytes);

    JSONObject actualJson = appEncryptionJsonImpl.decrypt("some data row record");
    assertTrue(expectedJson.similar(actualJson));
  }

  @Test
  void testEncrypt() {
    String expectedDataRowRecord = "some data row record";
    when(envelopeEncryption.encryptPayload(any())).thenReturn(expectedDataRowRecord);

    String actualDataRowRecord = appEncryptionJsonImpl.encrypt(new JSONObject("{\"some_key\": 123}"));
    assertEquals(expectedDataRowRecord, actualDataRowRecord);
  }

  @Test
  void testCloseSuccess() {
    appEncryptionJsonImpl.close();
    
    // Verify proper resources are closed
    verify(envelopeEncryption).close();
  }

  @Test
  void testCloseWithCloseFailShouldReturn() {
    doThrow(RuntimeException.class).when(envelopeEncryption).close();
    appEncryptionJsonImpl.close();
    
    verify(envelopeEncryption).close();
  }

}
