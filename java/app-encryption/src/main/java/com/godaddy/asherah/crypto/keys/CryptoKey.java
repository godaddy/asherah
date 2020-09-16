package com.godaddy.asherah.crypto.keys;

import java.time.Instant;
import java.util.function.Consumer;
import java.util.function.Function;

import com.godaddy.asherah.appencryption.utils.SafeAutoCloseable;

public abstract class CryptoKey implements SafeAutoCloseable {
  /**
   * Get the created time of the {@link CryptoKey}.
   * @return The created time of the CryptoKey.
   */
  public abstract Instant getCreated();

  /**
   * Checks if the {@link CryptoKey} is revoked.
   * @return {@code true} if the key is revoked, else false.
   */
  public abstract boolean isRevoked();

  /**
   * Marks the {@link CryptoKey} as revoked.
   */
  public abstract void markRevoked();

  /**
   * Performs an action with the {@link CryptoKey}.
   * @param action The action to be performed.
   */
  public abstract void withKey(Consumer<byte[]> action);

  /**
   * Applies a function to the key.
   * @param action The function to execute
   * @param <T> the type used to store the result of the function.
   * @return the result of the function.
   */
  public abstract <T> T withKey(Function<byte[], T> action);
}
