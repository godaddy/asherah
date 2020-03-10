package com.godaddy.asherah.securememory;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

import java.util.function.Consumer;
import java.util.function.Function;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

@ExtendWith(MockitoExtension.class)
class SecretTest {
  @Mock
  Secret secret;

  @SuppressWarnings("unchecked")
  @Test
  void testWithSecretBytesConsumerOfbyte() {
    byte[] secretBytes = new byte[]{0, 1};
    doAnswer(invocationOnMock -> ((Function<byte[], ?>) invocationOnMock.getArgument(0)).apply(secretBytes))
        .when(secret)
        .withSecretBytes(any(Function.class));
    Consumer<byte[]> consumer = mock(Consumer.class);
    doCallRealMethod().when(secret).withSecretBytes(any(Consumer.class));

    secret.withSecretBytes(consumer);
    verify(consumer).accept(secretBytes);
  }

  @SuppressWarnings("unchecked")
  @Test
  void testWithSecretUtf8CharsConsumerOfchar() {
    char[] secretChars = new char[]{0, 1};
    doAnswer(invocationOnMock -> ((Function<char[], ?>) invocationOnMock.getArgument(0)).apply(secretChars))
        .when(secret)
        .withSecretUtf8Chars(any(Function.class));
    Consumer<char[]> consumer = mock(Consumer.class);
    doCallRealMethod().when(secret).withSecretUtf8Chars(any(Consumer.class));

    secret.withSecretUtf8Chars(consumer);
    verify(consumer).accept(secretChars);
  }

}
