package com.godaddy.asherah.testapp.testhelpers;

public final class Constants {

  /*
  Test summary
   */
  public static final String TEST_ENDPOINT = "endpoint";
  public static final String TEST_SUMMARY_TOTAL_TESTS_COUNT = "total_count";
  public static final String TEST_SUMMARY_SUCCESSFUL_TESTS_COUNT = "success_count";
  public static final String TEST_SUMMARY_FAILURE_LIST = "failures";
  public static final String TEST_SUMMARY_FAILED_TESTS_COUNT = "failed_count";
  public static final String TEST_EXECUTION_TIMESTAMP = "timestamp";
  public static final String TEST_SUMMARY_METASTORE_TYPE = "metastore_type";
  public static final String TEST_SUMMARY_KEY_MANAGEMENT_TYPE = "kms_type";

  /*
  KMS
   */
  public static final String KEY_MANAGEMENT_STATIC = "static";
  public static final String KEY_MANAGEMENT_AWS = "aws";
  public static final String KEY_MANAGEMENT_STATIC_MASTER_KEY = "test_master_key_that_is_32_bytes";

  /*
  Metastore
   */
  public static final String METASTORE_IN_MEMORY = "memory";
  public static final String METASTORE_JDBC = "jdbc";
  public static final String METASTORE_DYNAMODB = "dynamodb";


  /*
  AWS
   */
  public static final String AWS_DEFAULT_PREFERRED_REGION = "us-west-2";

  /*
  IDs and Keys
   */
  public static final String DEFAULT_SYSTEM_ID = "system";
  public static final String DEFAULT_PRODUCT_ID = "product";
  public static final String DEFAULT_PARTITION_ID = "partition";
  public static final int KEY_EXPIRY_DAYS = 30;

  public static final String TEST_PARAM_NUM_ITERATIONS = "numIterations";
  public static final String TEST_PARAM_NUM_REQUESTS = "numRequests";
  public static final String TEST_PARAM_NUM_THREADS = "numThreads";
  public static final String TEST_PARAM_PAYLOAD_SIZE_BYTES = "payloadSizeBytes";
  public static final String TEST_PARAM_THREAD_POOL_TIMEOUT_SECONDS = "threadPoolTimeoutSeconds";

  private Constants() {
    // Do nothing
  }

}
