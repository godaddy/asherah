package com.godaddy.asherah.grpc;

public class DynamoDbConfig {
  private final String endpointConfig;
  private final String region;
  private final String keySuffix;
  private final String tableName;

  protected DynamoDbConfig(final String endpointConfig, final String region, final String keySuffix,
      final String tableName) {
    this.endpointConfig = endpointConfig;
    this.region = region;
    this.keySuffix = keySuffix;
    this.tableName = tableName;
  }

  /**
   * Getter for the field <code>endpointConfig</code>.
   * @return The end point for DynamoDb configuration.
   */
  public String getEndpointConfig() {
    return endpointConfig;
  }

  /**
   * Getter for the field <code>region</code>.
   * @return The region for DynamoDb configuration.
   */
  public String getRegion() {
    return region;
  }

  /**
   * Getter for the field <code>keySuffix</code>.
   * @return The key suffix to be used for DynamoDb Global Tables.
   */
  public String getKeySuffix() {
    return keySuffix;
  }

  /**
   * Getter for the field <code>tableName</code>.
   * @return The DynamoDb table name
   */
  public String getTableName() {
    return tableName;
  }
}
