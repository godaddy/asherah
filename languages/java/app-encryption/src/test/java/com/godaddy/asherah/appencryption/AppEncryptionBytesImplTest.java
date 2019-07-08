package com.godaddy.asherah.appencryption;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.godaddy.asherah.appencryption.envelope.EnvelopeEncryption;

@ExtendWith(MockitoExtension.class)
class AppEncryptionBytesImplTest {

  @Mock
  EnvelopeEncryption<String> envelopeEncryption;
  @InjectMocks
  AppEncryptionBytesImpl<String> appEncryptionBytesImpl;
  
  @Test
  void testConstructor() {
    AppEncryption<?, ?> appEncryption = new AppEncryptionBytesImpl<>(envelopeEncryption);
    assertNotNull(appEncryption);
  }

  @Test
  void testDecrypt() {
    byte[] expectedBytes = new byte[]{0, 1, 2, 3};
    when(envelopeEncryption.decryptDataRowRecord(any())).thenReturn(expectedBytes);

    byte[] actualBytes = appEncryptionBytesImpl.decrypt("some data row record");
    assertArrayEquals(expectedBytes, actualBytes);
  }

  @Test
  void testEncrypt() {
    String expectedDataRowRecord = "some data row record";
    when(envelopeEncryption.encryptPayload(any())).thenReturn(expectedDataRowRecord);

    String actualDataRowRecord = appEncryptionBytesImpl.encrypt(new byte[]{0, 1, 2, 3, 4});
    assertEquals(expectedDataRowRecord, actualDataRowRecord);
  }

  @Test
  void testCloseSuccess() {
    appEncryptionBytesImpl.close();
    
    // Verify proper resources are closed
    verify(envelopeEncryption).close();
  }

  @Test
  void testCloseWithCloseFailShouldReturn() {
    doThrow(RuntimeException.class).when(envelopeEncryption).close();
    appEncryptionBytesImpl.close();
    
    verify(envelopeEncryption).close();
  }

}
