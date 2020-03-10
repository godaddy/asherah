package com.godaddy.asherah.crypto.envelope;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.godaddy.asherah.crypto.keys.CryptoKey;

import java.time.Instant;
import java.util.Arrays;
import java.util.function.Function;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
public class AeadEnvelopeCryptoTest {

  @Mock
  private CryptoKey keyEncryptionKey;
  @Mock
  private CryptoKey key;
  @Mock
  private AeadEnvelopeCrypto aeadCrypto;

  @SuppressWarnings("unchecked")
  @Test
  public void testEncryptKey() {
    byte[] keyBytes = {0, 1, 2, 3};
    byte[] expectedEncryptedKey = new byte[]{4, 5, 6, 7};

    // We mock the withKey w/ Answer to call the function with our desired input param
    when(key.withKey(any(Function.class)))
        .thenAnswer(invocationOnMock ->
            ((Function<byte[], byte[]>) invocationOnMock.getArgument(0)).apply(keyBytes)
        );
    when(aeadCrypto.encrypt(eq(keyBytes), any())).thenReturn(expectedEncryptedKey);
    when(aeadCrypto.encryptKey(any(), any())).thenCallRealMethod();

    byte[] actualEncryptedKey = aeadCrypto.encryptKey(key, keyEncryptionKey);
    assertArrayEquals(expectedEncryptedKey, actualEncryptedKey);
    verify(key).withKey(any(Function.class));
    verify(aeadCrypto).encrypt(keyBytes, keyEncryptionKey);
  }

  @Test
  public void testDecryptKey() {
    byte[] encryptedKey = {0, 1, 2, 3, 4, 5, 6, 7, 8};
    Instant createdTime = Instant.now();
    when(aeadCrypto.decryptKey(eq(encryptedKey), eq(createdTime), eq(keyEncryptionKey), eq(false))).thenReturn(key);
    doCallRealMethod().when(aeadCrypto).decryptKey(any(), any(), any());

    CryptoKey actualKey = aeadCrypto.decryptKey(encryptedKey, createdTime, keyEncryptionKey);
    assertEquals(key, actualKey);
  }

  @Test
  public void testDecryptKeyWithRevoked() {
    byte[] encryptedKey = {0, 1, 2, 3, 4, 5, 6, 7, 8};
    byte[] decryptedKey = {8, 7, 6, 5, 4, 3, 2, 1, 0};
    byte[] expectedFinalKey = {0, 0, 0, 0, 0, 0, 0, 0, 0};
    Instant createdTime = Instant.now();
    boolean revoked = true;
    when(aeadCrypto.decrypt(encryptedKey, keyEncryptionKey)).thenReturn(decryptedKey);
    when(aeadCrypto.generateKeyFromBytes(decryptedKey, createdTime, revoked)).thenReturn(key);
    when(aeadCrypto.decryptKey(encryptedKey, createdTime, keyEncryptionKey, revoked)).thenCallRealMethod();
    assertNotEquals(Arrays.toString(expectedFinalKey), Arrays.toString(decryptedKey));

    CryptoKey actualKey = aeadCrypto.decryptKey(encryptedKey, createdTime, keyEncryptionKey, revoked);
    assertEquals(key, actualKey);
    assertArrayEquals(expectedFinalKey, decryptedKey);
  }

  @Test
  public void testEnvelopeEncrypt() {
    byte[] expectedEncryptedKey = {0, 1, 2, 3, 4, 5, 6, 7, 8};
    byte[] expectedPlainText = {1, 2, 3, 4, 5};
    byte[] expectedCipherText = {5, 4, 3, 2, 1};
    when(aeadCrypto.encrypt(expectedPlainText, key)).thenReturn(expectedCipherText);
    when(aeadCrypto.encryptKey(key, keyEncryptionKey)).thenReturn(expectedEncryptedKey);
    when(aeadCrypto.generateKey()).thenReturn(key);
    when(aeadCrypto.envelopeEncrypt(expectedPlainText, keyEncryptionKey, null)).thenCallRealMethod();

    EnvelopeEncryptResult result = aeadCrypto.envelopeEncrypt(expectedPlainText, keyEncryptionKey, null);
    assertArrayEquals(expectedCipherText, result.getCipherText());
    assertArrayEquals(expectedEncryptedKey, result.getEncryptedKey());
    assertNull(result.getUserState());
    verify(key).close();
  }

  @Test
  public void testEnvelopeEncryptWithTwoParams() {
    byte[] encryptedKey = {0, 1, 2, 3, 4, 5, 6, 7, 8};
    when(aeadCrypto.envelopeEncrypt(encryptedKey, keyEncryptionKey)).thenCallRealMethod();

    aeadCrypto.envelopeEncrypt(encryptedKey, keyEncryptionKey);
    verify(aeadCrypto).envelopeEncrypt(encryptedKey, keyEncryptionKey, null);
  }

  @Test
  public void testEnvelopeDecrypt() {
    CryptoKey plainText = mock(CryptoKey.class);
    byte[] cipherText = new byte[]{1, 2, 3, 4};
    byte[] expectedBytes = new byte[]{5, 6, 7, 8};
    byte[] encryptedKey = new byte[]{4, 5, 6, 7};
    Instant createdTime = Instant.now();

    when(aeadCrypto.decryptKey(encryptedKey, createdTime, keyEncryptionKey)).thenReturn(plainText);
    when(aeadCrypto.decrypt(cipherText, plainText)).thenReturn(expectedBytes);
    when(aeadCrypto.envelopeDecrypt(cipherText, encryptedKey, createdTime, keyEncryptionKey)).thenCallRealMethod();

    byte[] actualBytes = aeadCrypto.envelopeDecrypt(cipherText, encryptedKey, createdTime, keyEncryptionKey);
    verify(aeadCrypto).decrypt(cipherText, plainText);
    verify(aeadCrypto).decryptKey(encryptedKey, createdTime, keyEncryptionKey);
    assertArrayEquals(expectedBytes, actualBytes);
    verify(plainText).close();
  }
}
