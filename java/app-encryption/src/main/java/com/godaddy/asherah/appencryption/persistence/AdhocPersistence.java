package com.godaddy.asherah.appencryption.persistence;

import java.util.Optional;
import java.util.function.BiConsumer;
import java.util.function.Function;

/**
 * Convenient persistence implementation that allows functional interfaces to be passed in for load and store
 * implementations.
 *
 * @param <T> The type of the value being loaded and stored in the persistent store.
 */
public class AdhocPersistence<T> implements Persistence<T> {

  private final Function<String, Optional<T>> persistenceLoad;
  private final BiConsumer<String, T> persistenceStore;

  /**
   * Creates a new {@code AdhocPersistence} instance. The {@code load} and {@code store} functions need to be
   * implemented separately.
   *
   * @param load A user-defined {@link java.util.function.Function} object that loads a record from the persistent
   *             store.
   * @param store A user-defined {@link java.util.function.BiConsumer} object that stores a record to the persistent
   *              store.
   */
  public AdhocPersistence(final Function<String, Optional<T>> load, final BiConsumer<String, T> store) {
    this.persistenceLoad = load;
    this.persistenceStore = store;
  }

  @Override
  public Optional<T> load(final String key) {
    return persistenceLoad.apply(key);
  }

  @Override
  public void store(final String key, final T value) {
    persistenceStore.accept(key, value);
  }
}
