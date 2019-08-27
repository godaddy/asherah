package com.godaddy.asherah.testapp.utils;

import com.godaddy.asherah.appencryption.persistence.DynamoDbMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.InMemoryMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.JdbcMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.MetastorePersistence;
import com.godaddy.asherah.testapp.configuration.ServerConfiguration;
import com.zaxxer.hikari.HikariDataSource;

import static com.godaddy.asherah.testapp.testhelpers.Constants.METASTORE_DYNAMODB;
import static com.godaddy.asherah.testapp.testhelpers.Constants.METASTORE_JDBC;

import org.json.JSONObject;

public final class MetastoreFactory {

  private MetastoreFactory() {

  }

  public static MetastorePersistence<JSONObject> createMetastore(final ServerConfiguration configuration,
                                                                 final String metaStore) {
    if (metaStore.equalsIgnoreCase(METASTORE_JDBC)) {
      HikariDataSource dataSource = new HikariDataSource();
      dataSource.setJdbcUrl(configuration.getMetaStoreJdbcUrl());
      dataSource.setUsername(configuration.getMetaStoreJdbcUserName());
      dataSource.setPassword(configuration.getMetaStoreJdbcPassword());
      dataSource.setMaximumPoolSize(configuration.getMetaStoreJdbcConnectionPoolSize());
      return JdbcMetastoreImpl.newBuilder(dataSource).build();
    }
    else if (metaStore.equalsIgnoreCase(METASTORE_DYNAMODB)) {
      return DynamoDbMetastoreImpl.newBuilder().build();
    }
    else {
      return new InMemoryMetastoreImpl<>();
    }

  }
}
