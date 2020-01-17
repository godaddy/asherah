package com.godaddy.asherah.appencryption.envelope;

import com.godaddy.asherah.appencryption.Partition;
import com.godaddy.asherah.appencryption.exceptions.AppEncryptionException;
import com.godaddy.asherah.appencryption.exceptions.MetadataMissingException;
import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.appencryption.persistence.Metastore;
import com.godaddy.asherah.crypto.CryptoPolicy;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;
import com.godaddy.asherah.crypto.envelope.EnvelopeEncryptResult;
import com.godaddy.asherah.crypto.keys.CryptoKey;
import com.godaddy.asherah.crypto.keys.SecureCryptoKeyMap;
import com.google.common.collect.ImmutableMap;

import org.json.JSONObject;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.time.Instant;
import java.time.temporal.ChronoUnit;
import java.util.Base64;
import java.util.Optional;
import java.util.function.Function;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class EnvelopeEncryptionJsonImplTest {

  @Mock
  Metastore<JSONObject> metastore;
  @Mock
  SecureCryptoKeyMap<Instant> systemKeyCache;
  @Mock
  SecureCryptoKeyMap<Instant> intermediateKeyCache;
  @Mock
  AeadEnvelopeCrypto aeadEnvelopeCrypto;
  @Mock
  CryptoPolicy cryptoPolicy;
  @Mock
  KeyManagementService keyManagementService;

  EnvelopeEncryptionJsonImpl envelopeEncryptionJson;

  // Convenience mocks
  @Mock
  CryptoKey intermediateCryptoKey;
  @Mock
  CryptoKey systemCryptoKey;
  @Mock
  KeyMeta keyMeta;

  Partition partition = new Partition("shopper_123", "payments", "ecomm");

  // Setup Instants truncated to seconds and separated by hour to isolate overlap in case of interacting with multiple
  // level keys
  Instant drkInstant = Instant.now().truncatedTo(ChronoUnit.SECONDS);
  Instant ikInstant = drkInstant.minus(1, ChronoUnit.HOURS);
  Instant skInstant = ikInstant.minus(1, ChronoUnit.HOURS);

  @BeforeEach
  void setUp() {
    envelopeEncryptionJson = spy(new EnvelopeEncryptionJsonImpl(partition, metastore, systemKeyCache,
        intermediateKeyCache, aeadEnvelopeCrypto, cryptoPolicy, keyManagementService));
  }

  @SuppressWarnings("unchecked")
  @Test
  void testDecryptDataRowRecordWithParentKeyMetaShouldSucceed() {
    KeyMeta intermediateKeyMeta = new KeyMeta("parentKeyId", ikInstant);
    EnvelopeKeyRecord dataRowKey = new EnvelopeKeyRecord(drkInstant, intermediateKeyMeta, new byte[]{0, 1, 2, 3});
    byte[] encryptedData = new byte[]{4, 5, 6, 7};
    JSONObject dataRowRecord = new JSONObject(ImmutableMap.of(
      "Key", dataRowKey.toJson(),
      "Data", Base64.getEncoder().encodeToString(encryptedData)
    ));
    doAnswer(invocationOnMock -> ((Function<CryptoKey, byte[]>) invocationOnMock.getArgument(1))
        .apply(intermediateCryptoKey))
        .when(envelopeEncryptionJson)
        .withIntermediateKeyForRead(eq(intermediateKeyMeta), any(Function.class));
    byte[] expectedDecryptedPayload = new byte[]{11, 12, 13, 14};
    when(aeadEnvelopeCrypto
        .envelopeDecrypt(encryptedData, dataRowKey.getEncryptedKey(), dataRowKey.getCreated(), intermediateCryptoKey))
        .thenReturn(expectedDecryptedPayload);

    byte[] actualDecryptedPayload = envelopeEncryptionJson.decryptDataRowRecord(dataRowRecord);
    assertArrayEquals(expectedDecryptedPayload, actualDecryptedPayload);
    verify(aeadEnvelopeCrypto)
        .envelopeDecrypt(encryptedData, dataRowKey.getEncryptedKey(), dataRowKey.getCreated(), intermediateCryptoKey);
  }

  @Test
  void testDecryptDataRowRecordWithoutParentKeyMetaShouldFail() {
    EnvelopeKeyRecord dataRowKey = new EnvelopeKeyRecord(drkInstant, null, new byte[]{0, 1, 2, 3});
    byte[] encryptedData = new byte[]{4, 5, 6, 7};
    JSONObject dataRowRecord = new JSONObject(ImmutableMap.of(
      "Key", dataRowKey.toJson(),
      "Data", Base64.getEncoder().encodeToString(encryptedData)
    ));

    assertThrows(MetadataMissingException.class, () -> envelopeEncryptionJson.decryptDataRowRecord(dataRowRecord));
  }

  @SuppressWarnings("unchecked")
  @Test
  void testEncryptPayload() {
    when(intermediateCryptoKey.getCreated()).thenReturn(ikInstant);
    doAnswer(invocationOnMock -> ((Function<CryptoKey, EnvelopeEncryptResult>) invocationOnMock.getArgument(0))
        .apply(intermediateCryptoKey))
        .when(envelopeEncryptionJson)
        .withIntermediateKeyForWrite(any(Function.class));

    byte[] decryptedPayload = "somepayload".getBytes();
    KeyMeta intermediateKeyMeta = new KeyMeta(partition.getIntermediateKeyId(), ikInstant);
    byte[] encryptedPayload = new byte[]{0, 1, 2, 3};
    byte[] encryptedKey = new byte[]{4, 5, 6, 7};
    EnvelopeEncryptResult envelopeEncryptResult = new EnvelopeEncryptResult();
    envelopeEncryptResult.setCipherText(encryptedPayload);
    envelopeEncryptResult.setEncryptedKey(encryptedKey);
    envelopeEncryptResult.setUserState(intermediateKeyMeta);
    when(aeadEnvelopeCrypto.envelopeEncrypt(decryptedPayload, intermediateCryptoKey, intermediateKeyMeta))
      .thenReturn(envelopeEncryptResult);

    EnvelopeKeyRecord expectedDataRowKey = new EnvelopeKeyRecord(drkInstant, intermediateKeyMeta, encryptedKey);
    JSONObject expectedDataRowRecord = new JSONObject(ImmutableMap.of(
      "Key", expectedDataRowKey.toJson(),
      "Data", Base64.getEncoder().encodeToString(encryptedPayload)
    ));

    JSONObject actualDataRowRecord = envelopeEncryptionJson.encryptPayload(decryptedPayload);
    // Hate asserting like this but didn't want to inject Clock or add PowerMock for stubbing the Instant.now()...
    assertEquals(expectedDataRowRecord.getString("Data"), actualDataRowRecord.getString("Data"));
    assertEquals(expectedDataRowRecord.getJSONObject("Key").getString("Key"),
        actualDataRowRecord.getJSONObject("Key").getString("Key"));
    assertTrue(expectedDataRowRecord.getJSONObject("Key").getJSONObject("ParentKeyMeta")
        .similar(actualDataRowRecord.getJSONObject("Key").getJSONObject("ParentKeyMeta")));
  }

  @Test
  void testCloseSuccess() {
    envelopeEncryptionJson.close();

    // Verify proper resources are closed
    verify(intermediateKeyCache).close();
    verify(systemKeyCache, never()).close(); // shouldn't be closed
  }

  @Test
  void testCloseWithCloseFailShouldReturn() {
    doThrow(RuntimeException.class).when(intermediateKeyCache).close();
    envelopeEncryptionJson.close();

    verify(intermediateKeyCache).close();
    verify(systemKeyCache, never()).close(); // shouldn't be closed
  }

  @Test
  void testWithIntermediateKeyForReadWithKeyCachedAndNotExpiredShouldUseCache() {
    when(keyMeta.getCreated()).thenReturn(ikInstant);
    when(intermediateKeyCache.get(keyMeta.getCreated())).thenReturn(intermediateCryptoKey);

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithIntermediateKey = (cryptoKey) -> expectedBytes;

    byte[] actualBytes = envelopeEncryptionJson.withIntermediateKeyForRead(keyMeta, functionWithIntermediateKey);
    assertArrayEquals(expectedBytes, actualBytes);
    verify(envelopeEncryptionJson, never()).getIntermediateKey(any());
    verify(intermediateCryptoKey).close();
  }

  @Test
  void testWithIntermediateKeyForReadWithKeyCachedAndNotExpiredAndNotifyExpiredShouldUseCacheAndNotNotify() {
    when(keyMeta.getCreated()).thenReturn(ikInstant);
    when(intermediateKeyCache.get(keyMeta.getCreated())).thenReturn(intermediateCryptoKey);
    when(cryptoPolicy.notifyExpiredIntermediateKeyOnRead()).thenReturn(true);

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithIntermediateKey = (cryptoKey) -> expectedBytes;

    byte[] actualBytes = envelopeEncryptionJson.withIntermediateKeyForRead(keyMeta, functionWithIntermediateKey);
    assertArrayEquals(expectedBytes, actualBytes);
    verify(envelopeEncryptionJson, never()).getIntermediateKey(any());
    // TODO Add verify for notification not being called once implemented
    verify(intermediateCryptoKey).close();
  }

  @Test
  void testWithIntermediateKeyForReadWithKeyCachedAndExpiredAndNotifyExpiredShouldUseCacheAndNotify() {
    when(keyMeta.getCreated()).thenReturn(ikInstant);
    when(intermediateKeyCache.get(keyMeta.getCreated())).thenReturn(intermediateCryptoKey);
    doReturn(true).when(envelopeEncryptionJson).isKeyExpiredOrRevoked(intermediateCryptoKey);
    when(cryptoPolicy.notifyExpiredIntermediateKeyOnRead()).thenReturn(true);

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithIntermediateKey = (cryptoKey) -> expectedBytes;

    byte[] actualBytes = envelopeEncryptionJson.withIntermediateKeyForRead(keyMeta, functionWithIntermediateKey);
    assertArrayEquals(expectedBytes, actualBytes);
    verify(envelopeEncryptionJson, never()).getIntermediateKey(any());
    // TODO Add verify for notification being called once implemented
    verify(intermediateCryptoKey).close();
  }

  @Test
  void testWithIntermediateKeyForReadWithKeyNotCachedAndCannotCacheAndNotExpiredShouldLookup() {
    when(keyMeta.getCreated()).thenReturn(ikInstant);
    doReturn(intermediateCryptoKey).when(envelopeEncryptionJson).getIntermediateKey(any());

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithIntermediateKey = (cryptoKey) -> expectedBytes;

    byte[] actualBytes = envelopeEncryptionJson.withIntermediateKeyForRead(keyMeta, functionWithIntermediateKey);
    assertArrayEquals(expectedBytes, actualBytes);
    verify(envelopeEncryptionJson).getIntermediateKey(keyMeta.getCreated());
    verify(intermediateKeyCache, never()).putAndGetUsable(any(), any());
    verify(intermediateCryptoKey).close();
  }

  @Test
  void testWithIntermediateKeyForReadWithKeyNotCachedAndCanCacheAndNotExpiredShouldLookupAndCache() {
    when(keyMeta.getCreated()).thenReturn(ikInstant);
    doReturn(intermediateCryptoKey).when(envelopeEncryptionJson).getIntermediateKey(ikInstant);
    when(cryptoPolicy.canCacheIntermediateKeys()).thenReturn(true);
    when(intermediateCryptoKey.getCreated()).thenReturn(ikInstant);
    when(intermediateKeyCache.putAndGetUsable(intermediateCryptoKey.getCreated(), intermediateCryptoKey))
        .thenReturn(intermediateCryptoKey);

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithIntermediateKey = (cryptoKey) -> expectedBytes;

    byte[] actualBytes = envelopeEncryptionJson.withIntermediateKeyForRead(keyMeta, functionWithIntermediateKey);
    assertArrayEquals(expectedBytes, actualBytes);
    verify(envelopeEncryptionJson).getIntermediateKey(keyMeta.getCreated());
    verify(intermediateKeyCache).putAndGetUsable(intermediateCryptoKey.getCreated(), intermediateCryptoKey);
    verify(intermediateCryptoKey).close();
  }

  @Test
  void testWithIntermediateKeyForReadWithKeyNotCachedAndCanCacheAndCacheUpdateFailsShouldLookupAndFailAndCloseKey() {
    when(keyMeta.getCreated()).thenReturn(ikInstant);
    doReturn(intermediateCryptoKey).when(envelopeEncryptionJson).getIntermediateKey(any());
    when(cryptoPolicy.canCacheIntermediateKeys()).thenReturn(true);
    doThrow(new AppEncryptionException("fake exception")).when(intermediateKeyCache).putAndGetUsable(any(), any());

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithIntermediateKey = (cryptoKey) -> expectedBytes;

    assertThrows(AppEncryptionException.class, () ->
      envelopeEncryptionJson.withIntermediateKeyForRead(keyMeta, functionWithIntermediateKey));
    verify(envelopeEncryptionJson).getIntermediateKey(keyMeta.getCreated());
    verify(intermediateCryptoKey).close();
  }

  @Test
  void testWithIntermediateKeyForWriteWithKeyNotCachedAndCannotCacheShouldLookup() {
    doReturn(intermediateCryptoKey).when(envelopeEncryptionJson).getLatestOrCreateIntermediateKey();

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithIntermediateKey = (cryptoKey) -> expectedBytes;

    byte[] actualBytes = envelopeEncryptionJson.withIntermediateKeyForWrite(functionWithIntermediateKey);
    assertArrayEquals(expectedBytes, actualBytes);
    verify(envelopeEncryptionJson).getLatestOrCreateIntermediateKey();
    verify(intermediateKeyCache, never()).putAndGetUsable(any(), any());
    verify(intermediateCryptoKey).close();
  }

  @Test
  void testWithIntermediateKeyForWriteWithKeyNotCachedAndCanCacheShouldLookupAndCache() {
    doReturn(intermediateCryptoKey).when(envelopeEncryptionJson).getLatestOrCreateIntermediateKey();
    when(cryptoPolicy.canCacheIntermediateKeys()).thenReturn(true);
    when(intermediateCryptoKey.getCreated()).thenReturn(ikInstant);
    when(intermediateKeyCache.putAndGetUsable(intermediateCryptoKey.getCreated(), intermediateCryptoKey))
        .thenReturn(intermediateCryptoKey);

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithIntermediateKey = (cryptoKey) -> expectedBytes;

    byte[] actualBytes = envelopeEncryptionJson.withIntermediateKeyForWrite(functionWithIntermediateKey);
    assertArrayEquals(expectedBytes, actualBytes);
    verify(envelopeEncryptionJson).getLatestOrCreateIntermediateKey();
    verify(intermediateKeyCache).putAndGetUsable(intermediateCryptoKey.getCreated(), intermediateCryptoKey);
    verify(intermediateCryptoKey).close();
  }

  @Test
  void testWithIntermediateKeyForWriteWithKeyNotCachedAndCanCacheAndCacheUpdateFailsShouldLookupAndFailAndCloseKey() {
    doReturn(intermediateCryptoKey).when(envelopeEncryptionJson).getLatestOrCreateIntermediateKey();
    when(cryptoPolicy.canCacheIntermediateKeys()).thenReturn(true);
    doThrow(new AppEncryptionException("fake exception")).when(intermediateKeyCache).putAndGetUsable(any(), any());

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithIntermediateKey = (cryptoKey) -> expectedBytes;

    assertThrows(AppEncryptionException.class, () ->
      envelopeEncryptionJson.withIntermediateKeyForWrite(functionWithIntermediateKey));
    verify(envelopeEncryptionJson).getLatestOrCreateIntermediateKey();
    verify(intermediateCryptoKey).close();
  }

  @Test
  void testWithIntermediateKeyForWriteWithKeyCachedAndExpiredShouldLookup() {
    CryptoKey expiredCryptoKey = mock(CryptoKey.class);
    when(intermediateKeyCache.getLast()).thenReturn(expiredCryptoKey);
    doReturn(true).when(envelopeEncryptionJson).isKeyExpiredOrRevoked(expiredCryptoKey);
    doReturn(intermediateCryptoKey).when(envelopeEncryptionJson).getLatestOrCreateIntermediateKey();
    when(cryptoPolicy.canCacheIntermediateKeys()).thenReturn(true);
    when(intermediateCryptoKey.getCreated()).thenReturn(ikInstant);
    when(intermediateKeyCache.putAndGetUsable(intermediateCryptoKey.getCreated(), intermediateCryptoKey))
        .thenReturn(intermediateCryptoKey);

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithIntermediateKey = (cryptoKey) -> expectedBytes;

    byte[] actualBytes = envelopeEncryptionJson.withIntermediateKeyForWrite(functionWithIntermediateKey);
    assertArrayEquals(expectedBytes, actualBytes);

    verify(envelopeEncryptionJson).getLatestOrCreateIntermediateKey();
    verify(intermediateKeyCache).putAndGetUsable(intermediateCryptoKey.getCreated(), intermediateCryptoKey);
    verify(intermediateCryptoKey).close();
  }

  @Test
  void testWithIntermediateKeyForWriteWithKeyCachedAndNotExpiredShouldUseCache() {
    when(intermediateCryptoKey.getCreated()).thenReturn(ikInstant);
    when(intermediateKeyCache.getLast()).thenReturn(intermediateCryptoKey);

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithIntermediateKey = (cryptoKey) -> expectedBytes;

    byte[] actualBytes = envelopeEncryptionJson.withIntermediateKeyForWrite(functionWithIntermediateKey);
    assertArrayEquals(expectedBytes, actualBytes);

    verify(intermediateCryptoKey).close();
  }

  @Test
  void testWithExistingSystemKeyWithKeyCachedAndNotExpiredShouldUseCache() {
    when(keyMeta.getCreated()).thenReturn(skInstant);
    when(systemKeyCache.get(keyMeta.getCreated())).thenReturn(systemCryptoKey);

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithSystemKey = (cryptoKey) -> expectedBytes;

    byte[] actualBytes = envelopeEncryptionJson.withExistingSystemKey(keyMeta, false, functionWithSystemKey);
    assertArrayEquals(expectedBytes, actualBytes);
    verify(envelopeEncryptionJson, never()).getSystemKey(any());
    verify(systemCryptoKey).close();
  }

  @Test
  void
  testWithExistingSystemKeyWithKeyCachedAndExpiredAndNotTreatAsMissingAndNotNotifyExpiredShouldUseCacheAndNotNotify() {
    when(keyMeta.getCreated()).thenReturn(skInstant);
    when(systemKeyCache.get(keyMeta.getCreated())).thenReturn(systemCryptoKey);
    doReturn(true).when(envelopeEncryptionJson).isKeyExpiredOrRevoked(systemCryptoKey);

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithSystemKey = (cryptoKey) -> expectedBytes;

    byte[] actualBytes = envelopeEncryptionJson.withExistingSystemKey(keyMeta, false, functionWithSystemKey);
    assertArrayEquals(expectedBytes, actualBytes);
    verify(envelopeEncryptionJson, never()).getSystemKey(any());
    // TODO Add verify for notification not being called once implemented
    verify(systemCryptoKey).close();
  }

  @Test
  void testWithExistingSystemKeyWithKeyCachedAndExpiredAndNotTreatAsMissingAndNotifyExpiredShouldUseCacheAndNotify() {
    when(keyMeta.getCreated()).thenReturn(skInstant);
    when(systemKeyCache.get(keyMeta.getCreated())).thenReturn(systemCryptoKey);
    doReturn(true).when(envelopeEncryptionJson).isKeyExpiredOrRevoked(systemCryptoKey);
    when(cryptoPolicy.notifyExpiredSystemKeyOnRead()).thenReturn(true);

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithSystemKey = (cryptoKey) -> expectedBytes;

    byte[] actualBytes = envelopeEncryptionJson.withExistingSystemKey(keyMeta, false, functionWithSystemKey);
    assertArrayEquals(expectedBytes, actualBytes);
    verify(envelopeEncryptionJson, never()).getSystemKey(any());
    // TODO Add verify for notification being called once implemented
    verify(systemCryptoKey).close();
  }

  @Test
  void testWithExistingSystemKeyWithKeyCachedAndExpiredAndTreatAsMissingShouldThrowMetadataMissingException() {
    when(keyMeta.getCreated()).thenReturn(skInstant);
    when(systemKeyCache.get(keyMeta.getCreated())).thenReturn(systemCryptoKey);
    doReturn(true).when(envelopeEncryptionJson).isKeyExpiredOrRevoked(systemCryptoKey);

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithSystemKey = (cryptoKey) -> expectedBytes;

    assertThrows(MetadataMissingException.class, () ->
      envelopeEncryptionJson.withExistingSystemKey(keyMeta, true, functionWithSystemKey));
    verify(envelopeEncryptionJson, never()).getSystemKey(any());
    verify(systemCryptoKey).close();
  }

  @Test
  void testWithExistingSystemKeyWithKeyNotCachedAndCannotCacheAndNotExpiredShouldLookup() {
    doReturn(systemCryptoKey).when(envelopeEncryptionJson).getSystemKey(any());

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithSystemKey = (cryptoKey) -> expectedBytes;

    byte[] actualBytes = envelopeEncryptionJson.withExistingSystemKey(keyMeta, false, functionWithSystemKey);
    assertArrayEquals(expectedBytes, actualBytes);
    verify(envelopeEncryptionJson).getSystemKey(keyMeta);
    verify(systemKeyCache, never()).putAndGetUsable(any(), any());
    verify(systemCryptoKey).close();
  }

  @Test
  void testWithExistingSystemKeyWithKeyNotCachedAndCanCacheAndNotExpiredShouldLookupAndCache() {
    doReturn(systemCryptoKey).when(envelopeEncryptionJson).getSystemKey(any());
    when(cryptoPolicy.canCacheSystemKeys()).thenReturn(true);
    when(keyMeta.getCreated()).thenReturn(skInstant);
    when(systemKeyCache.putAndGetUsable(keyMeta.getCreated(), systemCryptoKey)).thenReturn(systemCryptoKey);

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithSystemKey = (cryptoKey) -> expectedBytes;

    byte[] actualBytes = envelopeEncryptionJson.withExistingSystemKey(keyMeta, false, functionWithSystemKey);
    assertArrayEquals(expectedBytes, actualBytes);
    verify(envelopeEncryptionJson).getSystemKey(keyMeta);
    verify(systemKeyCache).putAndGetUsable(keyMeta.getCreated(), systemCryptoKey);
    verify(systemCryptoKey).close();
  }

  @Test
  void testWithExistingSystemKeyWithKeyNotCachedAndCanCacheAndCacheUpdateFailsShouldLookupAndFailAndCloseKey() {
    doReturn(systemCryptoKey).when(envelopeEncryptionJson).getSystemKey(any());
    when(cryptoPolicy.canCacheSystemKeys()).thenReturn(true);
    doThrow(new AppEncryptionException("fake exception")).when(systemKeyCache).putAndGetUsable(any(), any());

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithSystemKey = (cryptoKey) -> expectedBytes;

    assertThrows(AppEncryptionException.class, () ->
      envelopeEncryptionJson.withExistingSystemKey(keyMeta, false, functionWithSystemKey));
    verify(envelopeEncryptionJson).getSystemKey(keyMeta);
    verify(systemCryptoKey).close();
  }

  @Test
  void testWithExistingSystemKeyWithKeyCachedAndNotExpiredAndFunctionThrowsErrorShouldThrowErrorAndCloseKey() {
    when(keyMeta.getCreated()).thenReturn(skInstant);
    when(systemKeyCache.get(keyMeta.getCreated())).thenReturn(systemCryptoKey);
    Function<CryptoKey, byte[]> functionWithSystemKey =
        (cryptoKey) -> {throw new AppEncryptionException("fake error");};

    assertThrows(AppEncryptionException.class, () ->
      envelopeEncryptionJson.withExistingSystemKey(keyMeta, false, functionWithSystemKey));
    verify(envelopeEncryptionJson, never()).getSystemKey(any());
    verify(systemCryptoKey).close();
  }

  @Test
  void testWithExistingSystemKeyWithKeyCachedAndNotExpiredAndCloseKeyThrowsErrorShouldThrowError() {
    when(keyMeta.getCreated()).thenReturn(skInstant);
    when(systemKeyCache.get(keyMeta.getCreated())).thenReturn(systemCryptoKey);
    Function<CryptoKey, byte[]> functionWithSystemKey = (cryptoKey) -> new byte[]{};
    doThrow(new AppEncryptionException("fake error")).when(systemCryptoKey).close();

    assertThrows(AppEncryptionException.class, () ->
      envelopeEncryptionJson.withExistingSystemKey(keyMeta, false, functionWithSystemKey));
    verify(envelopeEncryptionJson, never()).getSystemKey(any());
  }

  @Test
  void testWithSystemKeyForWriteWithKeyNotCachedAndCannotCacheShouldLookup() {
    doReturn(systemCryptoKey).when(envelopeEncryptionJson).getLatestOrCreateSystemKey();

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithSystemKey = (cryptoKey) -> expectedBytes;

    byte[] actualBytes = envelopeEncryptionJson.withSystemKeyForWrite(functionWithSystemKey);
    assertArrayEquals(expectedBytes, actualBytes);
    verify(envelopeEncryptionJson).getLatestOrCreateSystemKey();
    verify(systemKeyCache, never()).putAndGetUsable(any(), any());
    verify(systemCryptoKey).close();
  }

  @Test
  void testWithSystemKeyForWriteWithKeyNotCachedAndCanCacheShouldLookupAndCache() {
    doReturn(systemCryptoKey).when(envelopeEncryptionJson).getLatestOrCreateSystemKey();
    when(cryptoPolicy.canCacheSystemKeys()).thenReturn(true);
    when(systemCryptoKey.getCreated()).thenReturn(skInstant);
    when(systemKeyCache.putAndGetUsable(systemCryptoKey.getCreated(), systemCryptoKey)).thenReturn(systemCryptoKey);

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithSystemKey = (cryptoKey) -> expectedBytes;

    byte[] actualBytes = envelopeEncryptionJson.withSystemKeyForWrite(functionWithSystemKey);
    assertArrayEquals(expectedBytes, actualBytes);
    verify(envelopeEncryptionJson).getLatestOrCreateSystemKey();
    verify(systemKeyCache).putAndGetUsable(systemCryptoKey.getCreated(), systemCryptoKey);
    verify(systemCryptoKey).close();
  }

  @Test
  void testWithSystemKeyForWriteWithKeyNotCachedAndCanCacheAndCacheUpdateFailsShouldLookupAndFailAndCloseKey() {
    doReturn(systemCryptoKey).when(envelopeEncryptionJson).getLatestOrCreateSystemKey();
    when(cryptoPolicy.canCacheSystemKeys()).thenReturn(true);
    when(systemCryptoKey.getCreated()).thenReturn(skInstant);
    doThrow(new AppEncryptionException("fake exception")).when(systemKeyCache).putAndGetUsable(any(), any());

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithSystemKey = (cryptoKey) -> expectedBytes;

    assertThrows(AppEncryptionException.class, () ->
      envelopeEncryptionJson.withSystemKeyForWrite(functionWithSystemKey));
    verify(envelopeEncryptionJson).getLatestOrCreateSystemKey();
    verify(systemCryptoKey).close();
  }

  @Test
  void testWithSystemKeyForWriteWithKeyCachedAndExpiredShouldLookup() {
    CryptoKey expiredCryptoKey = mock(CryptoKey.class);
    when(systemKeyCache.getLast()).thenReturn(expiredCryptoKey);
    doReturn(true).when(envelopeEncryptionJson).isKeyExpiredOrRevoked(expiredCryptoKey);
    doReturn(systemCryptoKey).when(envelopeEncryptionJson).getLatestOrCreateSystemKey();
    when(cryptoPolicy.canCacheSystemKeys()).thenReturn(true);
    when(systemCryptoKey.getCreated()).thenReturn(skInstant);
    when(systemKeyCache.putAndGetUsable(systemCryptoKey.getCreated(), systemCryptoKey)).thenReturn(systemCryptoKey);

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithSystemKey = (cryptoKey) -> expectedBytes;

    byte[] actualBytes = envelopeEncryptionJson.withSystemKeyForWrite(functionWithSystemKey);
    assertArrayEquals(expectedBytes, actualBytes);

    verify(envelopeEncryptionJson).getLatestOrCreateSystemKey();
    verify(systemKeyCache).putAndGetUsable(systemCryptoKey.getCreated(), systemCryptoKey);
    verify(systemCryptoKey).close();
  }

  @Test
  void testWithSystemKeyForWriteWithKeyCachedAndNotExpiredShouldUseCache() {
    when(systemCryptoKey.getCreated()).thenReturn(skInstant);
    when(systemKeyCache.getLast()).thenReturn(systemCryptoKey);

    byte[] expectedBytes = new byte[] {0, 1, 2, 3};
    Function<CryptoKey, byte[]> functionWithSystemKey = (cryptoKey) -> expectedBytes;

    byte[] actualBytes = envelopeEncryptionJson.withSystemKeyForWrite(functionWithSystemKey);
    assertArrayEquals(expectedBytes, actualBytes);

    verify(systemCryptoKey).close();
  }

  @SuppressWarnings("unchecked")
  @Test
  void testGetLatestOrCreateIntermediateKeyWithEmptyMetastoreShouldCreateSuccessfully() {
    doReturn(Optional.empty()).when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());
    when(cryptoPolicy.truncateToIntermediateKeyPrecision(any())).thenReturn(ikInstant);
    when(aeadEnvelopeCrypto.generateKey(ikInstant)).thenReturn(intermediateCryptoKey);
    when(intermediateCryptoKey.getCreated()).thenReturn(ikInstant);
    doAnswer(invocationOnMock -> ((Function<CryptoKey, EnvelopeKeyRecord>) invocationOnMock.getArgument(0))
        .apply(systemCryptoKey))
        .when(envelopeEncryptionJson)
        .withSystemKeyForWrite(any(Function.class));
    when(aeadEnvelopeCrypto.encryptKey(any(), any())).thenReturn(new byte[]{0, 1, 2, 3});
    when(systemCryptoKey.getCreated()).thenReturn(skInstant);
    when(metastore.store(any(), any(), any())).thenReturn(true);

    CryptoKey actualIntermediateKey = envelopeEncryptionJson.getLatestOrCreateIntermediateKey();
    assertEquals(intermediateCryptoKey, actualIntermediateKey);
    verify(envelopeEncryptionJson, never()).decryptKey(any(), any());
    verify(aeadEnvelopeCrypto).encryptKey(intermediateCryptoKey, systemCryptoKey);
    verify(metastore).store(eq(partition.getIntermediateKeyId()), eq(ikInstant), any(JSONObject.class));
    verify(intermediateCryptoKey, never()).close();
  }

  @SuppressWarnings("unchecked")
  @Test
  void testGetLatestOrCreateIntermediateKeyWithEmptyMetastoreShouldAttemptCreateAndFailAndCloseKey() {
    doReturn(Optional.empty()).when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());
    when(cryptoPolicy.truncateToIntermediateKeyPrecision(any())).thenReturn(ikInstant);
    when(aeadEnvelopeCrypto.generateKey(ikInstant)).thenReturn(intermediateCryptoKey);
    when(intermediateCryptoKey.getCreated()).thenReturn(ikInstant);
    doAnswer(invocationOnMock -> ((Function<CryptoKey, EnvelopeKeyRecord>) invocationOnMock.getArgument(0))
        .apply(systemCryptoKey))
        .when(envelopeEncryptionJson)
        .withSystemKeyForWrite(any(Function.class));
    when(aeadEnvelopeCrypto.encryptKey(any(), any())).thenReturn(new byte[]{0, 1, 2, 3});
    when(systemCryptoKey.getCreated()).thenReturn(skInstant);
    doThrow(new AppEncryptionException("fake error")).when(metastore).store(any(), any(), any());

    assertThrows(AppEncryptionException.class, () -> envelopeEncryptionJson.getLatestOrCreateIntermediateKey());
    verify(envelopeEncryptionJson, never()).decryptKey(any(), any());
    verify(aeadEnvelopeCrypto).encryptKey(intermediateCryptoKey, systemCryptoKey);
    verify(intermediateCryptoKey).close();
  }

  @SuppressWarnings("unchecked")
  @Test
  void testGetLatestOrCreateIntermediateKeyWithLatestAndExpiredAndInlineRotationShouldCreateSuccessfully() {
    EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(ikInstant.minusSeconds(1),
      new KeyMeta("id", skInstant.minusSeconds(1)), new byte[]{0, 1, 2, 3}, false);
    doReturn(Optional.of(keyRecord)).when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());
    doReturn(true).when(envelopeEncryptionJson).isKeyExpiredOrRevoked(any(EnvelopeKeyRecord.class));
    when(cryptoPolicy.truncateToIntermediateKeyPrecision(any())).thenReturn(ikInstant);
    when(intermediateCryptoKey.getCreated()).thenReturn(ikInstant);
    when(aeadEnvelopeCrypto.generateKey(ikInstant)).thenReturn(intermediateCryptoKey);
    doAnswer(invocationOnMock -> ((Function<CryptoKey, EnvelopeKeyRecord>) invocationOnMock.getArgument(0))
        .apply(systemCryptoKey))
        .when(envelopeEncryptionJson)
        .withSystemKeyForWrite(any(Function.class));
    when(aeadEnvelopeCrypto.encryptKey(any(), any())).thenReturn(new byte[]{0, 1, 2, 3});
    when(systemCryptoKey.getCreated()).thenReturn(skInstant);
    when(metastore.store(any(), any(), any())).thenReturn(true);

    CryptoKey actualIntermediateKey = envelopeEncryptionJson.getLatestOrCreateIntermediateKey();
    assertEquals(intermediateCryptoKey, actualIntermediateKey);
    verify(envelopeEncryptionJson, never()).decryptKey(any(), any());
    verify(aeadEnvelopeCrypto).encryptKey(intermediateCryptoKey, systemCryptoKey);
    verify(metastore).store(eq(partition.getIntermediateKeyId()), eq(ikInstant), any(JSONObject.class));
    verify(intermediateCryptoKey, never()).close();
  }

  @SuppressWarnings("unchecked")
  @Test
  void testGetLatestOrCreateIntermediateKeyWithLatestAndNotExpiredShouldUseLatest() {
    EnvelopeKeyRecord keyRecord =
        new EnvelopeKeyRecord(ikInstant, new KeyMeta("id", skInstant), new byte[]{0, 1, 2, 3}, false);
    doReturn(Optional.of(keyRecord)).when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());

    doAnswer(invocationOnMock -> ((Function<CryptoKey, CryptoKey>) invocationOnMock.getArgument(2))
        .apply(systemCryptoKey))
        .when(envelopeEncryptionJson)
        .withExistingSystemKey(eq(keyRecord.getParentKeyMeta().get()), eq(true), any(Function.class));

    doReturn(intermediateCryptoKey).when(envelopeEncryptionJson).decryptKey(keyRecord, systemCryptoKey);

    CryptoKey actualIntermediateKey = envelopeEncryptionJson.getLatestOrCreateIntermediateKey();
    assertEquals(intermediateCryptoKey, actualIntermediateKey);
    verify(metastore, never()).store(any(), any(), any());
    verify(intermediateCryptoKey, never()).close();
  }

  @SuppressWarnings("unchecked")
  @Test
  void testGetLatestOrCreateIntermediateKeyWithLatestAndNotExpiredAndNoParentKeyMetaShouldCreateSuccessfully() {
    EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(ikInstant, null, new byte[]{0, 1, 2, 3}, false);
    doReturn(Optional.of(keyRecord)).when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());
    when(cryptoPolicy.truncateToIntermediateKeyPrecision(any())).thenReturn(ikInstant);
    when(intermediateCryptoKey.getCreated()).thenReturn(ikInstant);
    when(aeadEnvelopeCrypto.generateKey(ikInstant)).thenReturn(intermediateCryptoKey);
    doAnswer(invocationOnMock -> ((Function<CryptoKey, EnvelopeKeyRecord>) invocationOnMock.getArgument(0))
        .apply(systemCryptoKey))
        .when(envelopeEncryptionJson)
        .withSystemKeyForWrite(any(Function.class));
    when(aeadEnvelopeCrypto.encryptKey(any(), any())).thenReturn(new byte[]{0, 1, 2, 3});
    when(systemCryptoKey.getCreated()).thenReturn(skInstant);
    when(metastore.store(any(), any(), any())).thenReturn(true);

    CryptoKey actualIntermediateKey = envelopeEncryptionJson.getLatestOrCreateIntermediateKey();
    assertEquals(intermediateCryptoKey, actualIntermediateKey);
    verify(envelopeEncryptionJson, never()).decryptKey(any(), any());
    verify(aeadEnvelopeCrypto).encryptKey(intermediateCryptoKey, systemCryptoKey);
    verify(metastore).store(eq(partition.getIntermediateKeyId()), eq(ikInstant), any(JSONObject.class));
    verify(intermediateCryptoKey, never()).close();
  }

  @SuppressWarnings("unchecked")
  @Test
  void
  testGetLatestOrCreateIntermediateKeyWithLatestAndNotExpiredAndWithExistingSystemKeyFailsShouldCreateSuccessfully() {
    EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(ikInstant.minusSeconds(1),
        new KeyMeta("id", skInstant.minusSeconds(1)), new byte[]{0, 1, 2, 3}, false);
    doReturn(Optional.of(keyRecord)).when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());
    doThrow(MetadataMissingException.class)
        .when(envelopeEncryptionJson).withExistingSystemKey(any(), anyBoolean(), any());
    when(cryptoPolicy.truncateToIntermediateKeyPrecision(any())).thenReturn(ikInstant);
    when(intermediateCryptoKey.getCreated()).thenReturn(ikInstant);
    when(aeadEnvelopeCrypto.generateKey(ikInstant)).thenReturn(intermediateCryptoKey);
    doAnswer(invocationOnMock -> ((Function<CryptoKey, EnvelopeKeyRecord>) invocationOnMock.getArgument(0))
        .apply(systemCryptoKey))
        .when(envelopeEncryptionJson)
        .withSystemKeyForWrite(any(Function.class));
    when(aeadEnvelopeCrypto.encryptKey(any(), any())).thenReturn(new byte[]{0, 1, 2, 3});
    when(systemCryptoKey.getCreated()).thenReturn(skInstant);
    when(metastore.store(any(), any(), any())).thenReturn(true);

    CryptoKey actualIntermediateKey = envelopeEncryptionJson.getLatestOrCreateIntermediateKey();
    assertEquals(intermediateCryptoKey, actualIntermediateKey);
    verify(envelopeEncryptionJson, never()).decryptKey(any(), any());
    verify(aeadEnvelopeCrypto).encryptKey(intermediateCryptoKey, systemCryptoKey);
    verify(metastore).store(eq(partition.getIntermediateKeyId()), eq(ikInstant), any(JSONObject.class));
    verify(intermediateCryptoKey, never()).close();
  }

  @SuppressWarnings("unchecked")
  @Test
  void testGetLatestOrCreateIntermediateKeyWithLatestAndExpiredAndQueuedRotationShouldQueueAndUseLatest() {
    EnvelopeKeyRecord keyRecord =
        new EnvelopeKeyRecord(ikInstant, new KeyMeta("id", skInstant), new byte[]{0, 1, 2, 3}, false);
    doReturn(Optional.of(keyRecord)).when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());
    doReturn(true).when(envelopeEncryptionJson).isKeyExpiredOrRevoked(any(EnvelopeKeyRecord.class));
    when(cryptoPolicy.isQueuedKeyRotation()).thenReturn(true);
    doAnswer(invocationOnMock -> ((Function<CryptoKey, CryptoKey>) invocationOnMock.getArgument(2))
        .apply(systemCryptoKey))
        .when(envelopeEncryptionJson)
        .withExistingSystemKey(eq(keyRecord.getParentKeyMeta().get()), eq(true), any(Function.class));
    doReturn(intermediateCryptoKey).when(envelopeEncryptionJson).decryptKey(keyRecord, systemCryptoKey);

    CryptoKey actualIntermediateKey = envelopeEncryptionJson.getLatestOrCreateIntermediateKey();
    assertEquals(intermediateCryptoKey, actualIntermediateKey);
    // TODO Add verify for queue key rotation once implemented
    verify(metastore, never()).store(any(), any(), any());
    verify(intermediateCryptoKey, never()).close();
  }

  @SuppressWarnings("unchecked")
  @Test
  void
  testGetLatestOrCreateIntermediateKeyWithLatestAndExpiredAndQueuedRotationAndNoParentKeyMetaShouldCreateSuccessfully()
  {
    EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(ikInstant, null, new byte[]{0, 1, 2, 3}, false);
    doReturn(Optional.of(keyRecord)).when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());
    doReturn(true).when(envelopeEncryptionJson).isKeyExpiredOrRevoked(any(EnvelopeKeyRecord.class));
    when(cryptoPolicy.isQueuedKeyRotation()).thenReturn(true);
    when(cryptoPolicy.truncateToIntermediateKeyPrecision(any())).thenReturn(ikInstant);
    when(intermediateCryptoKey.getCreated()).thenReturn(ikInstant);
    when(aeadEnvelopeCrypto.generateKey(ikInstant)).thenReturn(intermediateCryptoKey);
    doAnswer(invocationOnMock -> ((Function<CryptoKey, EnvelopeKeyRecord>) invocationOnMock.getArgument(0))
        .apply(systemCryptoKey))
        .when(envelopeEncryptionJson)
        .withSystemKeyForWrite(any(Function.class));
    when(aeadEnvelopeCrypto.encryptKey(any(), any())).thenReturn(new byte[]{0, 1, 2, 3});
    when(systemCryptoKey.getCreated()).thenReturn(skInstant);
    when(metastore.store(any(), any(), any())).thenReturn(true);

    CryptoKey actualIntermediateKey = envelopeEncryptionJson.getLatestOrCreateIntermediateKey();
    assertEquals(intermediateCryptoKey, actualIntermediateKey);
    verify(envelopeEncryptionJson, never()).decryptKey(any(), any());
    // TODO Add verify for queue key rotation once implemented
    verify(aeadEnvelopeCrypto).encryptKey(intermediateCryptoKey, systemCryptoKey);
    verify(metastore).store(eq(partition.getIntermediateKeyId()), eq(ikInstant), any(JSONObject.class));
    verify(intermediateCryptoKey, never()).close();
  }

  @SuppressWarnings("unchecked")
  @Test
  void testGetLatestOrCreateIntermediateKeyWithLatestAndExpiredAndQueuedRotationAndWithExistingSystemKeyFailsShouldCreateSuccessfully() {
    EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(ikInstant.minusSeconds(1),
        new KeyMeta("id", skInstant.minusSeconds(1)), new byte[]{0, 1, 2, 3}, false);
    doReturn(Optional.of(keyRecord)).when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());
    doReturn(true).when(envelopeEncryptionJson).isKeyExpiredOrRevoked(any(EnvelopeKeyRecord.class));
    when(cryptoPolicy.isQueuedKeyRotation()).thenReturn(true);
    doThrow(MetadataMissingException.class)
        .when(envelopeEncryptionJson).withExistingSystemKey(any(), anyBoolean(), any());
    when(cryptoPolicy.truncateToIntermediateKeyPrecision(any())).thenReturn(ikInstant);
    when(intermediateCryptoKey.getCreated()).thenReturn(ikInstant);
    when(aeadEnvelopeCrypto.generateKey(ikInstant)).thenReturn(intermediateCryptoKey);
    doAnswer(invocationOnMock -> ((Function<CryptoKey, EnvelopeKeyRecord>) invocationOnMock.getArgument(0))
        .apply(systemCryptoKey))
        .when(envelopeEncryptionJson)
        .withSystemKeyForWrite(any(Function.class));
    when(aeadEnvelopeCrypto.encryptKey(any(), any())).thenReturn(new byte[]{0, 1, 2, 3});
    when(systemCryptoKey.getCreated()).thenReturn(skInstant);
    when(metastore.store(any(), any(), any())).thenReturn(true);

    CryptoKey actualIntermediateKey = envelopeEncryptionJson.getLatestOrCreateIntermediateKey();
    assertEquals(intermediateCryptoKey, actualIntermediateKey);
    verify(envelopeEncryptionJson, never()).decryptKey(any(), any());
    // TODO Add verify for queue key rotation once implemented
    verify(aeadEnvelopeCrypto).encryptKey(intermediateCryptoKey, systemCryptoKey);
    verify(metastore).store(eq(partition.getIntermediateKeyId()), eq(ikInstant), any(JSONObject.class));
    verify(intermediateCryptoKey, never()).close();
  }

  @SuppressWarnings("unchecked")
  @Test
  void testGetLatestOrCreateIntermediateKeyWithDuplicateKeyCreationAttemptShouldRetryOnce() {
    EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(ikInstant.minusSeconds(1),
      new KeyMeta("id", skInstant.minusSeconds(1)), new byte[]{0, 1, 2, 3}, false);
    CryptoKey unusedCryptoKey = mock(CryptoKey.class);

    doReturn(Optional.empty(), Optional.of(keyRecord))
      .when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());
    when(cryptoPolicy.truncateToIntermediateKeyPrecision(any())).thenReturn(ikInstant);
    when(systemCryptoKey.getCreated()).thenReturn(skInstant);
    when(aeadEnvelopeCrypto.generateKey(ikInstant)).thenReturn(unusedCryptoKey);
    when(unusedCryptoKey.getCreated()).thenReturn(ikInstant);
    doAnswer(invocationOnMock -> ((Function<CryptoKey, CryptoKey>) invocationOnMock.getArgument(2))
        .apply(systemCryptoKey))
        .when(envelopeEncryptionJson)
        .withExistingSystemKey(eq(keyRecord.getParentKeyMeta().get()), eq(true), any(Function.class));
    doAnswer(invocationOnMock -> ((Function<CryptoKey, EnvelopeKeyRecord>) invocationOnMock.getArgument(0))
        .apply(systemCryptoKey))
        .doAnswer(invocationOnMock -> ((Function<CryptoKey, CryptoKey>) invocationOnMock.getArgument(0))
            .apply(systemCryptoKey))
        .when(envelopeEncryptionJson)
        .withSystemKeyForWrite(any(Function.class));
    when(aeadEnvelopeCrypto.encryptKey(any(), any())).thenReturn(new byte[]{0, 1, 2, 3});
    when(metastore.store(any(), any(), any())).thenReturn(false);
    doReturn(intermediateCryptoKey).when(envelopeEncryptionJson).decryptKey(keyRecord, systemCryptoKey);

    CryptoKey actualIntermediateKey = envelopeEncryptionJson.getLatestOrCreateIntermediateKey();
    assertEquals(intermediateCryptoKey, actualIntermediateKey);
    verify(unusedCryptoKey).close();
  }

  @SuppressWarnings("unchecked")
  @Test
  void testGetLatestOrCreateIntermediateKeyWithDuplicateKeyCreationAttemptAndNoParentKeyMetaShouldFail() {
    EnvelopeKeyRecord keyRecord =
        new EnvelopeKeyRecord(ikInstant.minusSeconds(1), null, new byte[]{0, 1, 2, 3}, false);
    CryptoKey unusedCryptoKey = mock(CryptoKey.class);

    doReturn(Optional.empty(), Optional.of(keyRecord))
      .when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());
    when(cryptoPolicy.truncateToIntermediateKeyPrecision(any())).thenReturn(ikInstant);
    when(aeadEnvelopeCrypto.generateKey(ikInstant)).thenReturn(unusedCryptoKey);
    when(unusedCryptoKey.getCreated()).thenReturn(ikInstant);
    when(systemCryptoKey.getCreated()).thenReturn(skInstant);
    doAnswer(invocationOnMock -> ((Function<CryptoKey, EnvelopeKeyRecord>) invocationOnMock.getArgument(0))
        .apply(systemCryptoKey))
        .doAnswer(invocationOnMock -> ((Function<CryptoKey, CryptoKey>) invocationOnMock.getArgument(0))
            .apply(systemCryptoKey))
        .when(envelopeEncryptionJson)
        .withSystemKeyForWrite(any(Function.class));
    when(aeadEnvelopeCrypto.encryptKey(any(), any())).thenReturn(new byte[]{0, 1, 2, 3});
    when(metastore.store(any(), any(), any())).thenReturn(false);

    assertThrows(MetadataMissingException.class, () -> envelopeEncryptionJson.getLatestOrCreateIntermediateKey());
    verify(unusedCryptoKey).close();
  }

  @SuppressWarnings("unchecked")
  @Test
  void testGetLatestOrCreateIntermediateKeyWithDuplicateKeyCreationAttemptShouldRetryOnceButSecondTimeFails() {
    CryptoKey unusedCryptoKey = mock(CryptoKey.class);

    doReturn(Optional.empty())
      .when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());
    when(cryptoPolicy.truncateToIntermediateKeyPrecision(any())).thenReturn(ikInstant);
    when(aeadEnvelopeCrypto.generateKey(ikInstant)).thenReturn(unusedCryptoKey);
    when(unusedCryptoKey.getCreated()).thenReturn(ikInstant);
    when(systemCryptoKey.getCreated()).thenReturn(skInstant);
    doAnswer(invocationOnMock -> ((Function<CryptoKey, EnvelopeKeyRecord>) invocationOnMock.getArgument(0))
        .apply(systemCryptoKey))
        .when(envelopeEncryptionJson)
        .withSystemKeyForWrite(any(Function.class));
    when(metastore.store(any(), any(), any())).thenReturn(false);
    when(aeadEnvelopeCrypto.encryptKey(any(), any())).thenReturn(new byte[]{0, 1, 2, 3});

    assertThrows(AppEncryptionException.class, () -> envelopeEncryptionJson.getLatestOrCreateIntermediateKey());
    verify(unusedCryptoKey).close();
  }

  @Test
  void testGetLatestOrCreateSystemKeyWithEmptyMetastoreShouldCreateSuccessfully() {
    doReturn(Optional.empty()).when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());
    when(cryptoPolicy.truncateToSystemKeyPrecision(any())).thenReturn(skInstant);
    when(aeadEnvelopeCrypto.generateKey(skInstant)).thenReturn(systemCryptoKey);
    when(keyManagementService.encryptKey(systemCryptoKey)).thenReturn(new byte[]{0, 1, 2, 3});
    when(systemCryptoKey.getCreated()).thenReturn(skInstant);
    when(metastore.store(any(), any(), any())).thenReturn(true);

    CryptoKey actualSystemKey = envelopeEncryptionJson.getLatestOrCreateSystemKey();
    assertEquals(systemCryptoKey, actualSystemKey);
    verify(envelopeEncryptionJson, never()).decryptKey(any(), any());
    verify(keyManagementService).encryptKey(systemCryptoKey);
    verify(metastore).store(eq(partition.getSystemKeyId()), eq(skInstant), any(JSONObject.class));
    verify(systemCryptoKey, never()).close();
  }

  @Test
  void testGetLatestOrCreateSystemKeyWithEmptyMetastoreShouldAttemptCreateAndFailAndCloseKey() {
    doReturn(Optional.empty()).when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());
    when(cryptoPolicy.truncateToSystemKeyPrecision(any())).thenReturn(skInstant);
    when(aeadEnvelopeCrypto.generateKey(skInstant)).thenReturn(systemCryptoKey);
    when(systemCryptoKey.getCreated()).thenReturn(skInstant);
    when(keyManagementService.encryptKey(systemCryptoKey)).thenReturn(new byte[]{0, 1, 2, 3});
    doThrow(new AppEncryptionException("fake error")).when(metastore).store(any(), any(), any());

    assertThrows(AppEncryptionException.class, () -> envelopeEncryptionJson.getLatestOrCreateSystemKey());
    verify(envelopeEncryptionJson, never()).decryptKey(any(), any());
    verify(keyManagementService).encryptKey(systemCryptoKey);
    verify(systemCryptoKey).close();
  }

  @Test
  void testGetLatestOrCreateSystemKeyWithLatestAndExpiredAndInlineRotationShouldCreateSuccessfully() {
    EnvelopeKeyRecord keyRecord =
        new EnvelopeKeyRecord(skInstant.minusSeconds(1), null, new byte[]{0, 1, 2, 3}, false);
    doReturn(Optional.of(keyRecord)).when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());
    doReturn(true).when(envelopeEncryptionJson).isKeyExpiredOrRevoked(any(EnvelopeKeyRecord.class));
    when(cryptoPolicy.truncateToSystemKeyPrecision(any())).thenReturn(skInstant);
    when(aeadEnvelopeCrypto.generateKey(skInstant)).thenReturn(systemCryptoKey);
    when(systemCryptoKey.getCreated()).thenReturn(skInstant);
    when(keyManagementService.encryptKey(systemCryptoKey)).thenReturn(new byte[]{0, 1, 2, 3});
    when(metastore.store(any(), any(), any())).thenReturn(true);

    CryptoKey actualSystemKey = envelopeEncryptionJson.getLatestOrCreateSystemKey();
    assertEquals(systemCryptoKey, actualSystemKey);
    verify(keyManagementService, never()).decryptKey(any(), any(), anyBoolean());
    verify(keyManagementService).encryptKey(systemCryptoKey);
    verify(metastore).store(eq(partition.getSystemKeyId()), eq(skInstant), any(JSONObject.class));
    verify(systemCryptoKey, never()).close();
  }

  @Test
  void testGetLatestOrCreateSystemKeyWithLatestAndNotExpiredAndNonNullRevokedShouldUseLatest() {
    EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(skInstant, null, new byte[]{0, 1, 2, 3}, false);
    doReturn(false).when(envelopeEncryptionJson).isKeyExpiredOrRevoked(keyRecord);
    doReturn(Optional.of(keyRecord)).when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());
    when(keyManagementService
        .decryptKey(keyRecord.getEncryptedKey(), keyRecord.getCreated(), keyRecord.isRevoked().get()))
        .thenReturn(systemCryptoKey);

    CryptoKey actualSystemKey = envelopeEncryptionJson.getLatestOrCreateSystemKey();
    assertEquals(systemCryptoKey, actualSystemKey);
    verify(metastore, never()).store(any(), any(), any());
    verify(systemCryptoKey, never()).close();
  }

  @Test
  void testGetLatestOrCreateSystemKeyWithLatestAndNotExpiredAndNullRevokedShouldUseLatest() {
    EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(skInstant, null, new byte[]{0, 1, 2, 3}, null);
    doReturn(Optional.of(keyRecord)).when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());
    when(keyManagementService.decryptKey(keyRecord.getEncryptedKey(), keyRecord.getCreated(), false))
        .thenReturn(systemCryptoKey);

    CryptoKey actualSystemKey = envelopeEncryptionJson.getLatestOrCreateSystemKey();
    assertEquals(systemCryptoKey, actualSystemKey);
    verify(metastore, never()).store(any(), any(), any());
    verify(systemCryptoKey, never()).close();
  }

  @Test
  void testGetLatestOrCreateSystemKeyWithLatestAndExpiredAndQueuedRotationAndNonNullRevokedShouldQueueAndUseLatest() {
    EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(skInstant, null, new byte[]{0, 1, 2, 3}, true);
    doReturn(Optional.of(keyRecord)).when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());
    doReturn(true).when(envelopeEncryptionJson).isKeyExpiredOrRevoked(any(EnvelopeKeyRecord.class));
    when(cryptoPolicy.isQueuedKeyRotation()).thenReturn(true);
    when(keyManagementService.
        decryptKey(keyRecord.getEncryptedKey(), keyRecord.getCreated(), keyRecord.isRevoked().get()))
        .thenReturn(systemCryptoKey);

    CryptoKey actualSystemKey = envelopeEncryptionJson.getLatestOrCreateSystemKey();
    assertEquals(systemCryptoKey, actualSystemKey);
    // TODO Add verify for queue key rotation once implemented
    verify(metastore, never()).store(any(), any(), any());
    verify(systemCryptoKey, never()).close();
  }

  @Test
  void testGetLatestOrCreateSystemKeyWithLatestAndExpiredAndQueuedRotationAndNullRevokedShouldQueueAndUseLatest() {
    EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(skInstant, null, new byte[]{0, 1, 2, 3}, null);
    doReturn(Optional.of(keyRecord)).when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());
    doReturn(true).when(envelopeEncryptionJson).isKeyExpiredOrRevoked(any(EnvelopeKeyRecord.class));
    when(cryptoPolicy.isQueuedKeyRotation()).thenReturn(true);
    when(keyManagementService.decryptKey(keyRecord.getEncryptedKey(), keyRecord.getCreated(), false))
        .thenReturn(systemCryptoKey);

    CryptoKey actualSystemKey = envelopeEncryptionJson.getLatestOrCreateSystemKey();
    assertEquals(systemCryptoKey, actualSystemKey);
    // TODO Add verify for queue key rotation once implemented
    verify(metastore, never()).store(any(), any(), any());
    verify(systemCryptoKey, never()).close();
  }

  @Test
  void testGetLatestOrCreateSystemKeyWithDuplicateKeyCreationAttemptAndNonNullRevokedShouldRetryOnce() {
    EnvelopeKeyRecord keyRecord =
        new EnvelopeKeyRecord(skInstant.minusSeconds(1), null, new byte[]{0, 1, 2, 3}, true);
    CryptoKey unusedCryptoKey = mock(CryptoKey.class);

    doReturn(Optional.empty(), Optional.of(keyRecord))
      .when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());

    when(cryptoPolicy.truncateToSystemKeyPrecision(any())).thenReturn(skInstant);
    when(aeadEnvelopeCrypto.generateKey(skInstant)).thenReturn(unusedCryptoKey);
    when(keyManagementService.encryptKey(unusedCryptoKey)).thenReturn(new byte[]{0, 1, 2, 3});
    when(unusedCryptoKey.getCreated()).thenReturn(skInstant);
    when(metastore.store(any(), any(), any())).thenReturn(false);
    when(keyManagementService
        .decryptKey(keyRecord.getEncryptedKey(), keyRecord.getCreated(), keyRecord.isRevoked().get()))
        .thenReturn(systemCryptoKey);

    CryptoKey systemKey = envelopeEncryptionJson.getLatestOrCreateSystemKey();
    assertEquals(systemCryptoKey, systemKey);
    verify(unusedCryptoKey).close();
  }

  @Test
  void testGetLatestOrCreateSystemKeyWithDuplicateKeyCreationAttemptAndNullRevokedShouldRetryOnce() {
    EnvelopeKeyRecord keyRecord =
        new EnvelopeKeyRecord(skInstant.minusSeconds(1), null, new byte[]{0, 1, 2, 3}, null);
    CryptoKey unusedCryptoKey = mock(CryptoKey.class);

    doReturn(Optional.empty(), Optional.of(keyRecord))
      .when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());

    when(cryptoPolicy.truncateToSystemKeyPrecision(any())).thenReturn(skInstant);
    when(aeadEnvelopeCrypto.generateKey(skInstant)).thenReturn(unusedCryptoKey);
    when(keyManagementService.encryptKey(unusedCryptoKey)).thenReturn(new byte[]{0, 1, 2, 3});
    when(unusedCryptoKey.getCreated()).thenReturn(skInstant);
    when(metastore.store(any(), any(), any())).thenReturn(false);
    when(keyManagementService
        .decryptKey(keyRecord.getEncryptedKey(), keyRecord.getCreated(), false))
        .thenReturn(systemCryptoKey);

    CryptoKey systemKey = envelopeEncryptionJson.getLatestOrCreateSystemKey();
    assertEquals(systemCryptoKey, systemKey);
    verify(unusedCryptoKey).close();
  }

  @Test
  void testGetLatestOrCreateSystemKeyWithDuplicateKeyCreationAttemptShouldRetryOnceButSecondTimeFailsThrowsException() {
    CryptoKey unusedCryptoKey = mock(CryptoKey.class);

    doReturn(Optional.empty())
      .when(envelopeEncryptionJson).loadLatestKeyRecord(anyString());

    when(cryptoPolicy.truncateToSystemKeyPrecision(any())).thenReturn(skInstant);
    when(aeadEnvelopeCrypto.generateKey(skInstant)).thenReturn(unusedCryptoKey);
    when(keyManagementService.encryptKey(unusedCryptoKey)).thenReturn(new byte[]{0, 1, 2, 3});
    when(unusedCryptoKey.getCreated()).thenReturn(skInstant);
    when(metastore.store(any(), any(), any())).thenReturn(false);

    assertThrows(AppEncryptionException.class, () -> envelopeEncryptionJson.getLatestOrCreateSystemKey());
    verify(unusedCryptoKey).close();
  }

  @SuppressWarnings("unchecked")
  @Test
  void testGetIntermediateKeyWithParentKeyMetaShouldSucceed() {
    EnvelopeKeyRecord keyRecord =
        new EnvelopeKeyRecord(ikInstant, new KeyMeta("id", skInstant), new byte[]{0, 1, 2, 3}, false);
    doReturn(keyRecord).when(envelopeEncryptionJson).loadKeyRecord(any(), eq(ikInstant));
    doAnswer(invocationOnMock -> ((Function<CryptoKey, CryptoKey>) invocationOnMock.getArgument(2))
        .apply(systemCryptoKey))
        .when(envelopeEncryptionJson)
        .withExistingSystemKey(eq(keyRecord.getParentKeyMeta().get()), eq(false), any(Function.class));
    doReturn(intermediateCryptoKey).when(envelopeEncryptionJson).decryptKey(keyRecord, systemCryptoKey);

    CryptoKey actualIntermediateKey = envelopeEncryptionJson.getIntermediateKey(ikInstant);
    assertEquals(intermediateCryptoKey, actualIntermediateKey);
    verify(envelopeEncryptionJson)
        .withExistingSystemKey(eq(keyRecord.getParentKeyMeta().get()), eq(false), any(Function.class));
  }

  @Test
  void testGetIntermediateKeyWithoutParentKeyMetaShouldFail() {
    EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(ikInstant, null, new byte[]{0, 1, 2, 3}, false);
    doReturn(keyRecord).when(envelopeEncryptionJson).loadKeyRecord(any(), eq(ikInstant));

    assertThrows(MetadataMissingException.class, () -> envelopeEncryptionJson.getIntermediateKey(ikInstant));
  }

  @Test
  void testGetSystemKeyWithNonNullRevoked() {
    EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(skInstant, null, new byte[]{0, 1, 2, 3}, true);
    doReturn(keyRecord).when(envelopeEncryptionJson).loadKeyRecord(any(), any());

    envelopeEncryptionJson.getSystemKey(keyMeta);
    verify(keyManagementService).
        decryptKey(keyRecord.getEncryptedKey(), keyRecord.getCreated(), keyRecord.isRevoked().get());
  }

  @Test
  void testGetSystemKeyWithNullRevoked() {
    EnvelopeKeyRecord keyRecord = new EnvelopeKeyRecord(skInstant, null, new byte[]{0, 1, 2, 3}, null);
    doReturn(keyRecord).when(envelopeEncryptionJson).loadKeyRecord(any(), any());

    envelopeEncryptionJson.getSystemKey(keyMeta);
    verify(keyManagementService).decryptKey(keyRecord.getEncryptedKey(), keyRecord.getCreated(), false);
  }

  @Test
  void testDecryptKeyWithNonNullRevoked() {
    EnvelopeKeyRecord keyRecord =
        new EnvelopeKeyRecord(ikInstant, new KeyMeta("id", skInstant), new byte[]{0, 1, 2, 3}, true);

    envelopeEncryptionJson.decryptKey(keyRecord, intermediateCryptoKey);
    verify(aeadEnvelopeCrypto)
        .decryptKey(keyRecord.getEncryptedKey(), keyRecord.getCreated(), intermediateCryptoKey, true);
  }

  @Test
  void testDecryptKeyWithNullRevoked() {
    EnvelopeKeyRecord keyRecord =
        new EnvelopeKeyRecord(ikInstant, new KeyMeta("id", skInstant), new byte[]{0, 1, 2, 3}, null);

    envelopeEncryptionJson.decryptKey(keyRecord, intermediateCryptoKey);
    verify(aeadEnvelopeCrypto)
        .decryptKey(keyRecord.getEncryptedKey(), keyRecord.getCreated(), intermediateCryptoKey, false);
  }

  @Test
  void testLoadKeyRecord() {
    byte[] pretendKeyBytes = {0, 1, 2, 3, 4, 5, 6, 7};
    EnvelopeKeyRecord envelopeKeyRecord =
        new EnvelopeKeyRecord(ikInstant, new KeyMeta("KeyId", skInstant), pretendKeyBytes, false);

    when(metastore.load(any(), any())).thenReturn(Optional.ofNullable(envelopeKeyRecord.toJson()));

    EnvelopeKeyRecord returnedEnvelopeKeyRecord = envelopeEncryptionJson.loadKeyRecord("empty", ikInstant);
    assertEquals(envelopeKeyRecord.getCreated(), returnedEnvelopeKeyRecord.getCreated());
    assertEquals(envelopeKeyRecord.getParentKeyMeta(), returnedEnvelopeKeyRecord.getParentKeyMeta());
    assertArrayEquals(envelopeKeyRecord.getEncryptedKey(), returnedEnvelopeKeyRecord.getEncryptedKey());
  }

  @Test
  void testLoadKeyRecordMissingItem() {
    when(metastore.load(any(), any())).thenReturn(Optional.empty());

    assertThrows(MetadataMissingException.class, () -> {
      envelopeEncryptionJson.loadKeyRecord("empty", Instant.now());
    });
  }

  @Test
  void testLoadLatestKeyRecord() {
    byte[] pretendKeyBytes = {0, 1, 2, 3, 4, 5, 6, 7};
    EnvelopeKeyRecord envelopeKeyRecord =
        new EnvelopeKeyRecord(ikInstant, new KeyMeta("KeyId", skInstant), pretendKeyBytes, false);

    when(metastore.loadLatest(any())).thenReturn(Optional.ofNullable(envelopeKeyRecord.toJson()));

    Optional<EnvelopeKeyRecord> returnedOptionalEnvelopeKeyRecord = envelopeEncryptionJson.loadLatestKeyRecord("empty");
    assertTrue(returnedOptionalEnvelopeKeyRecord.isPresent());
    assertEquals(ikInstant, returnedOptionalEnvelopeKeyRecord.get().getCreated());
    assertEquals(envelopeKeyRecord.getParentKeyMeta(), returnedOptionalEnvelopeKeyRecord.get().getParentKeyMeta());
    assertArrayEquals(envelopeKeyRecord.getEncryptedKey(), returnedOptionalEnvelopeKeyRecord.get().getEncryptedKey());
  }

  @Test
  void testLoadLatestKeyRecordEmptyResult() {
    when(metastore.loadLatest(any())).thenReturn(Optional.empty());

    assertFalse(envelopeEncryptionJson.loadLatestKeyRecord("empty").isPresent());
  }

  @Test
  void testIsKeyExpiredOrRevokedEnvelopeKeyRecordWithExpired() {
    Instant now = Instant.now();
    EnvelopeKeyRecord record = new EnvelopeKeyRecord(now, null, new byte[]{0, 1});
    when(cryptoPolicy.isKeyExpired(now)).thenReturn(true);

    assertTrue(envelopeEncryptionJson.isKeyExpiredOrRevoked(record));
  }

  @Test
  void testIsKeyExpiredOrRevokedEnvelopeKeyRecordWithNotExpiredAndRevoked() {
    EnvelopeKeyRecord record = new EnvelopeKeyRecord(Instant.now(), null, new byte[]{0, 1}, true);

    assertTrue(envelopeEncryptionJson.isKeyExpiredOrRevoked(record));
  }

  @Test
  void testIsKeyExpiredOrRevokedEnvelopeKeyRecordWithNotExpiredAndNotRevoked() {
    EnvelopeKeyRecord record = new EnvelopeKeyRecord(Instant.now(), null, new byte[]{0, 1});

    assertFalse(envelopeEncryptionJson.isKeyExpiredOrRevoked(record));
  }

  @Test
  void testIsKeyExpiredOrRevokedEnvelopeKeyRecordWithNotExpiredAndNullRevokedShouldDefaultToFalse() {
    EnvelopeKeyRecord record = new EnvelopeKeyRecord(Instant.now(), null, new byte[]{0, 1}, null);

    assertFalse(envelopeEncryptionJson.isKeyExpiredOrRevoked(record));
  }

  @Test
  void testIsKeyExpiredOrRevokedCryptoKeyWithExpired() {
    Instant now = Instant.now();
    when(systemCryptoKey.getCreated()).thenReturn(now);
    when(cryptoPolicy.isKeyExpired(now)).thenReturn(true);

    assertTrue(envelopeEncryptionJson.isKeyExpiredOrRevoked(systemCryptoKey));
  }

  @Test
  void testIsKeyExpiredOrRevokedCryptoKeyWithNotExpiredAndRevoked() {
    when(systemCryptoKey.isRevoked()).thenReturn(true);

    assertTrue(envelopeEncryptionJson.isKeyExpiredOrRevoked(systemCryptoKey));
  }

  @Test
  void testIsKeyExpiredOrRevokedCryptoKeyWithNotExpiredAndNotRevoked() {
    assertFalse(envelopeEncryptionJson.isKeyExpiredOrRevoked(systemCryptoKey));
  }

}
