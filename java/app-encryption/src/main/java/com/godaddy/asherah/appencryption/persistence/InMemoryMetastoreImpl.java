package com.godaddy.asherah.appencryption.persistence;

import java.time.Instant;
import java.util.Optional;
import java.util.SortedMap;

import com.google.common.collect.TreeBasedTable;

/**
 * Provides a {@link TreeBasedTable} based implementation of {@link Metastore} to store and retrieve
 * {@link com.godaddy.asherah.appencryption.utils.Json} values for system and intermediate keys.
 * NOTE: This is a volatile implementation and should NEVER be used in a production environment.
 *
 * @param <T> The type of the value being loaded and stored in the metastore.
 */
public class InMemoryMetastoreImpl<T> implements Metastore<T> {
  private final TreeBasedTable<String, Instant, T> table = TreeBasedTable.create();

  @Override
  public Optional<T> load(final String keyId, final Instant created) {
    synchronized (table) {
      return Optional.ofNullable(table.get(keyId, created));
    }
  }

  @Override
  public Optional<T> loadLatest(final String keyId) {
    synchronized (table) {
      SortedMap<Instant, T> partitionMap = table.row(keyId);
      // Need to check if empty as lastKey will throw an exception instead of returning null
      if (partitionMap.isEmpty()) {
        return Optional.empty();
      }

      return Optional.of(partitionMap.get(partitionMap.lastKey()));
    }
  }

  @Override
  public boolean store(final String keyId, final Instant created, final T value) {
    synchronized (table) {

      if (table.contains(keyId, created)) {
        return false;
      }

      table.put(keyId, created, value);
      return true;
    }
  }
}
