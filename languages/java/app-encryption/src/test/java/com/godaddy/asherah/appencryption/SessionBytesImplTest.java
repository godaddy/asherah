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
class SessionBytesImplTest {

  @Mock
  EnvelopeEncryption<String> envelopeEncryption;
  @InjectMocks
  SessionBytesImpl<String> sessionBytesImpl;

  @Test
  void testConstructor() {
    Session<?, ?> session = new SessionBytesImpl<>(envelopeEncryption);
    assertNotNull(session);
  }

  @Test
  void testDecrypt() {
    byte[] expectedBytes = new byte[]{0, 1, 2, 3};
    when(envelopeEncryption.decryptDataRowRecord(any())).thenReturn(expectedBytes);

    byte[] actualBytes = sessionBytesImpl.decrypt("some data row record");
    assertArrayEquals(expectedBytes, actualBytes);
  }

  @Test
  void testEncrypt() {
    String expectedDataRowRecord = "some data row record";
    when(envelopeEncryption.encryptPayload(any())).thenReturn(expectedDataRowRecord);

    String actualDataRowRecord = sessionBytesImpl.encrypt(new byte[]{0, 1, 2, 3, 4});
    assertEquals(expectedDataRowRecord, actualDataRowRecord);
  }

  @Test
  void testCloseSuccess() {
    sessionBytesImpl.close();

    // Verify proper resources are closed
    verify(envelopeEncryption).close();
  }

  @Test
  void testCloseWithCloseFailShouldReturn() {
    doThrow(RuntimeException.class).when(envelopeEncryption).close();
    sessionBytesImpl.close();

    verify(envelopeEncryption).close();
  }

}
