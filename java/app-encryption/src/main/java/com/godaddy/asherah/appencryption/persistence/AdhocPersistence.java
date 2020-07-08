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
