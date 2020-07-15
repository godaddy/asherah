package com.godaddy.asherah;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.dataformat.yaml.YAMLFactory;
import com.godaddy.asherah.appencryption.exceptions.AppEncryptionException;
import com.godaddy.asherah.appencryption.kms.AwsKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.appencryption.kms.StaticKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.persistence.DynamoDbMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.InMemoryMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.JdbcMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.Metastore;
import com.google.common.base.Splitter;
import com.zaxxer.hikari.HikariDataSource;
import org.apache.commons.lang3.StringUtils;
import org.json.JSONObject;

import java.io.File;
import java.io.IOException;
import java.util.Map;

import static com.godaddy.asherah.testhelpers.Constants.*;

public class TestSetup {

  public static Metastore<JSONObject> createMetastore() {
    String metastoreType = configReader().getMetastoreType();
    if (metastoreType.equalsIgnoreCase(METASTORE_JDBC)) {
      String jdbcUrl = configReader().getMetastoreJdbcUrl();
      if (StringUtils.isEmpty(jdbcUrl)) {
        throw new AppEncryptionException("Missing JDBC connection string");
      }

      HikariDataSource dataSource = new HikariDataSource();
      dataSource.setJdbcUrl(jdbcUrl);

      return JdbcMetastoreImpl.newBuilder(dataSource).build();
    }

    if (metastoreType.equalsIgnoreCase(METASTORE_DYNAMODB)) {
      return DynamoDbMetastoreImpl.newBuilder("us-west-2").build();
    }

    return new InMemoryMetastoreImpl<>();
  }

  public static KeyManagementService createKeyManagemementService() {
    String kmsType = configReader().getKmsType();
    if (kmsType.equalsIgnoreCase(KEY_MANAGEMENT_AWS)) {
      String regionToArnTuples = configReader().getKmsAwsRegionArnTuples();
      if (StringUtils.isEmpty(regionToArnTuples)) {
        throw new AppEncryptionException("Missing AWS Region ARN tuples");
      }

      Map<String, String> regionToArnMap = Splitter.on(',').withKeyValueSeparator('=').split(regionToArnTuples);

      String preferredRegion = configReader().getKmsAwsPreferredRegion();
      if (StringUtils.isEmpty(preferredRegion)) {
        preferredRegion = AWS_DEFAULT_PREFERRED_REGION;
      }

      return AwsKeyManagementServiceImpl.newBuilder(regionToArnMap, preferredRegion).build();
    }

    return new StaticKeyManagementServiceImpl(KEY_MANAGEMENT_STATIC_MASTER_KEY);
  }

  private static TestConfiguration configReader() {
    ObjectMapper mapper = new ObjectMapper(new YAMLFactory());
    TestConfiguration testConfiguration = null;
    try {
      testConfiguration = mapper.readValue(new File("src/it/resources/config.yaml"), TestConfiguration.class);
    } catch (IOException e) {
      e.printStackTrace();
    }
    return testConfiguration;
  }
}
