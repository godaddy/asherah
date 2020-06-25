package com.godaddy.asherah.appencryption;

import java.util.Optional;

import com.godaddy.asherah.appencryption.persistence.Persistence;
import com.godaddy.asherah.appencryption.utils.SafeAutoCloseable;

/**
 * Primary interface for using the app encryption library.
 *
 * @param <P> The payload type of the data being encrypted (e.g. JSON, String, etc.).
 * @param <D> The Data Row Record type being used to store it and any supporting metadata.
 */
public interface Session<P, D> extends SafeAutoCloseable {

  /**
   * Uses a persistence key to load a Data Row Record from the provided data persistence store, if any, and returns the
   * decrypted payload.
   *
   * @param persistenceKey The key to lookup in the data persistence store.
   * @param dataPersistence The data persistence store to use.
   * @return The decrypted payload, if found in persistence.
   */
  default Optional<P> load(final String persistenceKey, final Persistence<D> dataPersistence) {
    return dataPersistence.load(persistenceKey)
        .map(this::decrypt);
  }

  /**
   * Encrypts a payload, stores the resulting Data Row Record into the provided data persistence store, and returns its
   * associated persistence key for future lookups.
   *
   * @param payload The payload to encrypt and store.
   * @param dataPersistence The data persistence store to use.
   * @return The persistence key associated with the stored Data Row Record.
   */
  default String store(final P payload, final Persistence<D> dataPersistence) {
    D dataRowRecord = encrypt(payload);

    return dataPersistence.store(dataRowRecord);
  }

  /**
   * Encrypts a payload, stores the resulting Data Row Record into the provided data persistence store with given key.
   *
   * @param key The key to associate the Data Row Record with.
   * @param payload The payload to encrypt and store.
   * @param dataPersistence The data persistence store to use.
   */
  default void store(final String key, final P payload, final Persistence<D> dataPersistence) {
    D dataRowRecord = encrypt(payload);

    dataPersistence.store(key, dataRowRecord);
  }

  /**
   * Decrypts a Data Row Record based on an implementation-specific encryption algorithm and returns the actual payload.
   *
   * @param dataRowRecord The Data Row Record to decrypt.
   * @return The decrypted payload.
   */
  P decrypt(D dataRowRecord);

  /**
   * Encrypts a payload using an implementation-specific encryption algorithm and returns the Data Row Record that
   * contains it.
   *
   * @param payload The payload to encrypt.
   * @return The Data Row Record that contains the now-encrypted payload.
   */
  D encrypt(P payload);
}

