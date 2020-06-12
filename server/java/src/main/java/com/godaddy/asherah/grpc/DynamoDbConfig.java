package com.godaddy.asherah.grpc;

public class DynamoDbConfig {
  private String dynamoDbEndpointConfig;

  private String dynamoDbSigningRegion;
  private String dynamoDbRegion;
  private String keySuffix;
  private String tableName;

  protected DynamoDbConfig(final String dynamoDbEndpointConfig,
                        final String dynamoDbSigningRegion,
                        final String dynamoDbRegion,
                        final String keySuffix,
                        final String tableName) {
    this.dynamoDbEndpointConfig = dynamoDbEndpointConfig;
    this.dynamoDbSigningRegion = dynamoDbSigningRegion;
    this.dynamoDbRegion = dynamoDbRegion;
    this.keySuffix = keySuffix;
    this.tableName = tableName;
  }

  public String getDynamoDbEndpointConfig() {
    return dynamoDbEndpointConfig;
  }

  public String getDynamoDbSigningRegion() {
    return dynamoDbSigningRegion;
  }

  public String getDynamoDbRegion() {
    return dynamoDbRegion;
  }

  public String getKeySuffix() {
    return keySuffix;
  }

  public String getTableName() {
    return tableName;
  }
}
