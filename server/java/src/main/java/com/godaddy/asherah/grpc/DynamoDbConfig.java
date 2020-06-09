package com.godaddy.asherah.grpc;

public class DynamoDbConfig {
  private static String[] dynamoDbEndpointConfig;
  private static String dynamoDbRegion;
  private static String regionSuffix;
  private static String tableName;

  protected DynamoDbConfig(final String[] dynamoDbEndpointConfig,
                        final String dynamoDbRegion,
                        final String regionSuffix,
                        final String tableName) {
    this.dynamoDbEndpointConfig = dynamoDbEndpointConfig;
    this.dynamoDbRegion = dynamoDbRegion;
    this.regionSuffix = regionSuffix;
    this.tableName = tableName;
  }

  public static String[] getDynamoDbEndpointConfig() {
    return dynamoDbEndpointConfig;
  }

  public static String getDynamoDbRegion() {
    return dynamoDbRegion;
  }

  public static String getRegionSuffix() {
    return regionSuffix;
  }

  public static String getTableName() {
    return tableName;
  }
}
