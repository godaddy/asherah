package com.godaddy.asherah.crypto.keys;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class SecureCryptoKeyMapTest {

  @Mock
  private SharedCryptoKey sharedCryptoKey;
  @Mock
  private CryptoKey cryptoKey;
  private long revokeCheckPeriodMillis = 1000;

  private SecureCryptoKeyMap<String> secureCryptoKeyMap;

  @BeforeEach
  void setUp() {
    secureCryptoKeyMap = new SecureCryptoKeyMap<>(revokeCheckPeriodMillis);
  }

  @AfterEach
  void tearDown() {
    secureCryptoKeyMap.close();
  }

  @Test
  void testGetWithNullSecret() {
    String key = "null_key";
    assertNull(secureCryptoKeyMap.get(key));
  }

  @Test
  void testGetWithRevokedKeyShouldReturnKey() {
    String key = "some_key";
    when(sharedCryptoKey.isRevoked()).thenReturn(true);
    secureCryptoKeyMap.putAndGetUsable(key, sharedCryptoKey);

    CryptoKey actualCryptoKey = secureCryptoKeyMap.get(key);
    assertTrue(actualCryptoKey instanceof SharedCryptoKey);
    assertEquals(sharedCryptoKey, ((SharedCryptoKey) actualCryptoKey).getSharedKey());
    assertTrue(actualCryptoKey.isRevoked());
  }

  @Test
  void testGetWithRevokeCheckExpiredShouldReturnNull() throws Exception {
    try (SecureCryptoKeyMap<String> secureCryptoKeyMap = new SecureCryptoKeyMap<>(1)) {
      String key = "some_key";
      secureCryptoKeyMap.putAndGetUsable(key, sharedCryptoKey);
      // sleep to trigger period check flow
      Thread.sleep(3);

      CryptoKey actualCryptoKey = secureCryptoKeyMap.get(key);
      assertNull(actualCryptoKey);
    }
  }

  @Test
  void testGetLastWithEmptyMapShouldReturnNull() {
    assertNull(secureCryptoKeyMap.getLast());
  }

  @Test
  void testGetLastWithRevokedKeyShouldReturnKey() {
    String key = "some_key";
    when(sharedCryptoKey.isRevoked()).thenReturn(true);
    secureCryptoKeyMap.putAndGetUsable(key, sharedCryptoKey);

    CryptoKey actualCryptoKey = secureCryptoKeyMap.getLast();
    assertTrue(actualCryptoKey instanceof SharedCryptoKey);
    assertEquals(sharedCryptoKey, ((SharedCryptoKey) actualCryptoKey).getSharedKey());
    assertTrue(actualCryptoKey.isRevoked());
  }

  @Test
  void testGetLastWithRevokeCheckExpiredShouldReturnNull() throws Exception {
    try (SecureCryptoKeyMap<String> secureCryptoKeyMap = new SecureCryptoKeyMap<>(1)) {
      String key = "some_key";
      secureCryptoKeyMap.putAndGetUsable(key, sharedCryptoKey);
      // sleep to trigger period check flow
      Thread.sleep(3);

      CryptoKey actualCryptoKey = secureCryptoKeyMap.getLast();
      assertNull(actualCryptoKey);
    }
  }

  @Test
  void testSimplePutAndGet() {
    String key = "some_key";
    secureCryptoKeyMap.putAndGetUsable(key, sharedCryptoKey);
    CryptoKey actualCryptoKey = secureCryptoKeyMap.get(key);
    assertNotNull(actualCryptoKey);
    assertTrue(actualCryptoKey instanceof SharedCryptoKey);
  }

  @Test
  void testPutMultipleAndGetLast() {
    CryptoKey cryptoKey1 = mock(CryptoKey.class);
    CryptoKey cryptoKey2 = mock(CryptoKey.class);
    CryptoKey cryptoKey3 = mock(CryptoKey.class);
    CryptoKey cryptoKey4 = mock(CryptoKey.class);
    CryptoKey cryptoKey5 = mock(CryptoKey.class);
    secureCryptoKeyMap.putAndGetUsable("klhjasdffghs", cryptoKey1);
    secureCryptoKeyMap.putAndGetUsable("zzzzzzzz", cryptoKey2); // should always be last since sorted
    secureCryptoKeyMap.putAndGetUsable("ghtew", cryptoKey3);
    secureCryptoKeyMap.putAndGetUsable("asdfasdfasdf", cryptoKey4);
    secureCryptoKeyMap.putAndGetUsable("aaaaaaaa", cryptoKey5);

    CryptoKey lastCryptoKey = secureCryptoKeyMap.getLast();
    assertTrue(lastCryptoKey instanceof SharedCryptoKey);
    assertEquals(cryptoKey2, ((SharedCryptoKey) lastCryptoKey).getSharedKey());
  }

  @Test
  void testDuplicatePutAndGetUsable() {
    CryptoKey returnValue = secureCryptoKeyMap.putAndGetUsable("test", cryptoKey);
    assertNotEquals(cryptoKey, returnValue);
    assertTrue(returnValue instanceof SharedCryptoKey);

    CryptoKey returnValueTwo = secureCryptoKeyMap.putAndGetUsable("test", cryptoKey);
    assertEquals(cryptoKey, returnValueTwo);
  }

  @Test
  void testPutAndGetUsableWithNotRevokedShouldUpdateReturnNullAfterCheckPeriodAndNotNullAfterPutRefreshes()
      throws Exception {
    // Give it enough time to account for timing differences
    try (SecureCryptoKeyMap<String> secureCryptoKeyMap = new SecureCryptoKeyMap<>(50)) {
      secureCryptoKeyMap.putAndGetUsable("test", cryptoKey);
      CryptoKey getResult = secureCryptoKeyMap.get("test");
      assertNotNull(getResult);

      // Sleep for enough time to get null result
      Thread.sleep(100);
      getResult = secureCryptoKeyMap.get("test");
      assertNull(getResult);

      // Put back in to refresh cached time so we can get non-null result again
      secureCryptoKeyMap.putAndGetUsable("test", cryptoKey);
      getResult = secureCryptoKeyMap.get("test");
      assertNotNull(getResult);
    }
  }

  @Test
  void testPutAndGetUsableWithUpdateRevokedShouldMarkRevokedAndReturnNotNull() throws Exception {
    // Give it enough time to account for timing differences
    try (SecureCryptoKeyMap<String> secureCryptoKeyMap = new SecureCryptoKeyMap<>(50)) {
      secureCryptoKeyMap.putAndGetUsable("test", cryptoKey);
      CryptoKey getResult = secureCryptoKeyMap.get("test");
      assertNotNull(getResult);

      // Sleep for enough time to get null result
      Thread.sleep(100);
      getResult = secureCryptoKeyMap.get("test");
      assertNull(getResult);

      // Put back in as revoked so we can get non-null result again
      when(cryptoKey.isRevoked()).thenReturn(true);
      secureCryptoKeyMap.putAndGetUsable("test", cryptoKey);
      getResult = secureCryptoKeyMap.get("test");
      assertNotNull(getResult);
      // Sleep for enough time to get null result and verify we still get non-null
      Thread.sleep(30);
      getResult = secureCryptoKeyMap.get("test");
      assertNotNull(getResult);
    }
  }

  @Test
  void testKeyCloseIsCalledOnKeyMapClose() {
    secureCryptoKeyMap.putAndGetUsable("some_key", sharedCryptoKey);

    secureCryptoKeyMap.close();

    verify(sharedCryptoKey).close();
  }

  @Test
  void testKeyCloseIsCalledOnKeyMapCloseAndSecondCloseIsNoop() {
    secureCryptoKeyMap.putAndGetUsable("some_key", sharedCryptoKey);

    secureCryptoKeyMap.close();
    secureCryptoKeyMap.close();

    verify(sharedCryptoKey).close();
  }

  @Test
  void testKeyMapThrowsExceptionOnGetAfterClose() {
    String key = "some_key";
    secureCryptoKeyMap.putAndGetUsable(key, sharedCryptoKey);
    secureCryptoKeyMap.close();

    assertThrows(IllegalStateException.class, () -> secureCryptoKeyMap.get(key));
  }

  @Test
  void testKeyMapThrowsExceptionOnGetLastAfterClose() {
    secureCryptoKeyMap.close();

    assertThrows(IllegalStateException.class, () -> secureCryptoKeyMap.getLast());
  }

  @Test
  void testKeyMapThrowsExceptionOnPutAfterClose() {
    String key = "some_key";
    secureCryptoKeyMap.putAndGetUsable(key, sharedCryptoKey);
    secureCryptoKeyMap.close();

    assertThrows(IllegalStateException.class, () -> secureCryptoKeyMap.putAndGetUsable(key, sharedCryptoKey));
  }
}
