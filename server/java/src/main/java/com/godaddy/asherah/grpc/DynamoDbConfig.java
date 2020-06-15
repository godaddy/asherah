package com.godaddy.asherah.grpc;

public class DynamoDbConfig {
  private final String endpointConfig;
  private final String signingRegion;
  private final String region;
  private final String keySuffix;
  private final String tableName;

  protected DynamoDbConfig(final String endpointConfig,
                        final String signingRegion,
                        final String region,
                        final String keySuffix,
                        final String tableName) {
    this.endpointConfig = endpointConfig;
    this.signingRegion = signingRegion;
    this.region = region;
    this.keySuffix = keySuffix;
    this.tableName = tableName;
  }

  public String getEndpointConfig() {
    return endpointConfig;
  }

  public String getSigningRegion() {
    return signingRegion;
  }

  public String getRegion() {
    return region;
  }

  public String getKeySuffix() {
    return keySuffix;
  }

  public String getTableName() {
    return tableName;
  }
}
