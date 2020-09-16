package com.godaddy.asherah.crypto.keys;

import java.util.function.Consumer;
import java.util.function.Function;

import com.godaddy.asherah.securememory.Secret;

import java.time.Instant;

public class SecretCryptoKey extends CryptoKey {
  private final Secret secret;
  private final Instant created;
  private volatile boolean revoked;

  SecretCryptoKey(final CryptoKey otherKey) {
    SecretCryptoKey cryptoKey = (SecretCryptoKey) otherKey;
    this.secret = cryptoKey.getSecret().copySecret();
    this.created = otherKey.getCreated();
    this.revoked = otherKey.isRevoked();
  }

  /**
   * Creates a new {@code SecretCryptoKey}.
   * @param secret a {@link Secret} object.
   * @param created The creation time of the key.
   * @param revoked Indicates if the key is revoked.
   */
  public SecretCryptoKey(final Secret secret, final Instant created, final boolean revoked) {
    this.secret = secret;
    this.created = created;
    this.revoked = revoked;
  }

  @Override
  public <T> T withKey(final Function<byte[], T> action) {
    return secret.withSecretBytes(action);
  }

  @Override
  public void withKey(final Consumer<byte[]> action) {
    secret.withSecretBytes(action);
  }

  @Override
  public Instant getCreated() {
    return created;
  }

  @Override
  public boolean isRevoked() {
    return revoked;
  }

  @Override
  public void markRevoked() {
    revoked = true;
  }

  @Override
  public void close() {
    secret.close();
  }

  Secret getSecret() {
    return secret;
  }

}
