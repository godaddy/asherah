package com.godaddy.asherah.grpc;

public class DynamoDbConfig {
  private static String dynamoDbEndpointConfig;

  private static String dynamoDbSigningRegion;
  private static String dynamoDbRegion;
  private static String keySuffix;
  private static String tableName;

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

  public static String getDynamoDbEndpointConfig() {
    return dynamoDbEndpointConfig;
  }

  public static String getDynamoDbSigningRegion() {
    return dynamoDbSigningRegion;
  }

  public static String getDynamoDbRegion() {
    return dynamoDbRegion;
  }

  public static String getKeySuffix() {
    return keySuffix;
  }

  public static String getTableName() {
    return tableName;
  }
}
