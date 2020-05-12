package com.godaddy.asherah.cltf;

final class Constants {

  private Constants() {
  }

  public static final String KeyManagementStaticMasterKey = "test_master_key_that_is_32_bytes";

  public static final String DefaultServiceId = "service";
  public static final String DefaultProductId = "product";
  public static final String DefaultPartitionId = "partition";

  public static final String FileDirectory = "/tmp/";
  public static final String FileName = "java_encrypted";

  public static final int KeyExpiryDays = 30;
  public static final int RevokeCheckMinutes = 60;

  private static final String MysqlDatbaseName = System.getenv("TEST_DB_NAME");
  private static final String MysqlUsername = System.getenv("TEST_DB_USER");
  private static final String MysqlPassword = System.getenv("TEST_DB_PASSWORD");
  protected static final String JdbcConnectionString = "jdbc:mysql://localhost/" +
      MysqlDatbaseName + "?" +
      "user=" + MysqlUsername +
      "&password=" + MysqlPassword;
}
