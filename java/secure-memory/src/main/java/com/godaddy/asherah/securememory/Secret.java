package com.godaddy.asherah.securememory;

import java.util.function.Consumer;
import java.util.function.Function;

public interface Secret {
  <T> T withSecretBytes(Function<byte[], T> function);

  <T> T withSecretUtf8Chars(Function<char[], T> function);

  default void withSecretBytes(Consumer<byte[]> consumer) {
    this.withSecretBytes(bytes -> {
      consumer.accept(bytes);
      return null;
    });
  }

  default void withSecretUtf8Chars(Consumer<char[]> consumer) {
    this.withSecretUtf8Chars(chars -> {
      consumer.accept(chars);
      return null;
    });
  }

  Secret copySecret();

  void close();
}
