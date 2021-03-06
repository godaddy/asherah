package com.godaddy.asherah.cltf;

final class Constants {

  private Constants() {
  }

  public static final String KeyManagementStaticMasterKey = "thisIsAStaticMasterKeyForTesting";

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
  private static final String MysqlPort = System.getenv("TEST_DB_PORT");
  protected static final String JdbcConnectionString = "jdbc:mysql://localhost:" + MysqlPort + "/" + MysqlDatbaseName
      + "?" + "user=" + MysqlUsername + "&password=" + MysqlPassword;
}
