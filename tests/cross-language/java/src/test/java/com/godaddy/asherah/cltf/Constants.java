package com.godaddy.asherah.cltf;

final class Constants {

  private Constants() {
  }

  public static final String KeyManagementStaticMasterKey = "mysupersecretstaticmasterkey!!!!";

  public static final String DefaultServiceId = "service";
  public static final String DefaultProductId = "product";
  public static final String DefaultPartitionId = "partition";

  public static final String FileDirectory = "/tmp/";
  public static final String FileName = "java_encrypted";

  public static final int KeyExpiryDays = 30;
  public static final int RevokeCheckMinutes = 60;

  private static String mysqlDatbaseName = System.getenv("TEST_DB_NAME");
  private static String mysqlUsername = System.getenv("TEST_DB_USER");
  private static String mysqlPassword = System.getenv("TEST_DB_PASSWORD");
  protected static String jdbcConnectionString = "jdbc:mysql://localhost/" +
      mysqlDatbaseName + "?" +
      "user=" + mysqlUsername +
      "&password=" + mysqlPassword;
}
