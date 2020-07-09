package com.godaddy.asherah;

public class TestConfiguration {

  private String metastoreType;
  private String metastoreJdbcUrl;
  private String kmsType;
  private String kmsAwsRegionArnTuples;
  private String kmsAwsPreferredRegion;

  public String getMetastoreType() {
    return metastoreType;
  }

  public String getMetastoreJdbcUrl() {
    return metastoreJdbcUrl;
  }

  public String getKmsType() {
    return kmsType;
  }

  public String getKmsAwsRegionArnTuples() {
    return kmsAwsRegionArnTuples;
  }

  public String getKmsAwsPreferredRegion() {
    return kmsAwsPreferredRegion;
  }
}
