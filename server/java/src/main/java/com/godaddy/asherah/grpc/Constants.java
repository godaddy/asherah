package com.godaddy.asherah.grpc;

final class Constants {

  private Constants() {
  }

  static final String DRR_DATA = "Data";
  static final String DRR_KEY = "Key";

  static final String EKR_PARENTKEYMETA = "ParentKeyMeta";
  static final String EKR_KEY = "Key";
  static final String EKR_CREATED = "Created";

  static final String PARENTKEYMETA_KEYID = "KeyId";
  static final String PARENTKEYMETA_CREATED = "Created";

  static final String METASTORE_INMEMORY = "MEMORY";
  static final String METASTORE_JDBC = "JDBC";
  static final String METASTORE_DYNAMODB = "DYNAMODB";

  static final String KMS_STATIC = "STATIC";
  static final String KMS_AWS = "AWS";

  static final String DEFAULT_UDS_PATH = "/tmp/appencryption.sock";
  static final int DEFAULT_SERVER_TIMEOUT = 30;
}
