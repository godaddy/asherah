package com.godaddy.asherah.crypto.keys;

import java.time.Instant;
import java.util.function.Consumer;
import java.util.function.Function;

import com.godaddy.asherah.appencryption.utils.SafeAutoCloseable;

public abstract class CryptoKey implements SafeAutoCloseable {
  public abstract Instant getCreated();

  public abstract boolean isRevoked();

  public abstract void markRevoked();

  public abstract void withKey(Consumer<byte[]> action);

  public abstract <T> T withKey(Function<byte[], T> action);
}
