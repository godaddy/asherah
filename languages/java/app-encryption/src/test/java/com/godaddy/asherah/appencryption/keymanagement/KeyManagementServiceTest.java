package com.godaddy.asherah.appencryption.keymanagement;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

import java.time.Instant;
import java.util.function.BiFunction;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.godaddy.asherah.crypto.keys.CryptoKey;

@ExtendWith(MockitoExtension.class)
class KeyManagementServiceTest {
  @Mock
  CryptoKey cryptoKey;
  @Mock
  KeyManagementService keyManagementService;

  @Test
  void testWithDecryptedKey() {
    byte[] keyCipherText = new byte[]{0, 1};
    Instant now = Instant.now();
    boolean revoked = false;
    String expectedResult = "success";
    BiFunction<CryptoKey, Instant, String> actionWithDecryptedKey = (key, instant) -> {
      if (cryptoKey.equals(key) && now.equals(instant)) {
        return expectedResult;
      }
      else {
        return "failure";
      }
    };
    when(keyManagementService.decryptKey(keyCipherText, now, revoked)).thenReturn(cryptoKey);
    when(keyManagementService.withDecryptedKey(any(), any(), anyBoolean(), any())).thenCallRealMethod();

    String actualResult = keyManagementService.withDecryptedKey(keyCipherText, now, revoked, actionWithDecryptedKey);
    assertEquals(expectedResult, actualResult);
  }

}
