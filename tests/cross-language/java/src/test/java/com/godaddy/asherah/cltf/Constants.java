package com.godaddy.asherah.cltf;

final class Constants {

  private Constants() {
  }

  public static final String KeyManagementStaticMasterKey = "mysupersecretstaticmasterkey!!!!";

  public static final String JdbcConnectionString = "jdbc:mysql://127.0.0.1/test";
  public static final String User = "root";
  public static final String Password = "Password123";

  public static final String DefaultServiceId = "service";
  public static final String DefaultProductId = "product";
  public static final String DefaultPartitionId = "partition";

  public static final String FileDirectory = "encrypted_files";
  public static final String FileName = "java_encrypted";

  public static final int KeyExpiryDays = 30;
  public static final int RevokeCheckMinutes = 60;
}
