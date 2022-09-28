package com.godaddy.asherah.grpc;

public class DynamoDbConfig {
  private final String endpointConfig;
  private final String region;
  private final boolean enableKeySuffix;
  private final String tableName;

  protected DynamoDbConfig(final String endpointConfig, final String region, final boolean enableKeySuffix,
      final String tableName) {
    this.endpointConfig = endpointConfig;
    this.region = region;
    this.enableKeySuffix = enableKeySuffix;
    this.tableName = tableName;
  }

  public String getEndpointConfig() {
    return endpointConfig;
  }

  public String getRegion() {
    return region;
  }

  public boolean getKeySuffixEnabled() {
    return enableKeySuffix;
  }

  public String getTableName() {
    return tableName;
  }
}
