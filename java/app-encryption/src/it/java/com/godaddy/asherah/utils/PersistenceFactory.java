package com.godaddy.asherah.utils;

import com.godaddy.asherah.appencryption.persistence.Persistence;

import java.util.Map;
import java.util.Optional;
import java.util.concurrent.ConcurrentHashMap;

public final class PersistenceFactory {

  private PersistenceFactory() {
  }

  public static <T> Persistence<T> createInMemoryPersistence() {
    return new Persistence<T>() {
      private final Map<String, T> mapPersistence = new ConcurrentHashMap<>();

      @Override
      public Optional<T> load(final String key) {
        return Optional.ofNullable(mapPersistence.get(key));
      }

      @Override
      public void store(final String key, final T value) {
        mapPersistence.put(key, value);
      }
    };
  }
}
