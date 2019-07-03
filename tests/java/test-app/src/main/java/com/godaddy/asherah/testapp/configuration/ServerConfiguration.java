package com.godaddy.asherah.testapp.configuration;

import com.amazonaws.regions.Region;
import com.amazonaws.regions.Regions;
import com.google.common.base.Splitter;

import org.hibernate.validator.constraints.NotEmpty;

import static com.godaddy.asherah.testapp.testhelpers.Constants.*;

import java.util.Map;

public class ServerConfiguration extends io.dropwizard.Configuration {

  private boolean exportResultsToLog;
  private boolean exportResultsToElasticsearch;
  private boolean exportResultsToKinesis;
  @NotEmpty
  private String elasticsearchUrl;
  private String elasticsearchIndexPrefix;
  private String kinesisDataStream;
  private String kmsType = KEY_MANAGEMENT_STATIC;
  private String kmsAwsRegionMap;
  private String kmsAwsPreferredRegion = null;
  private String metaStoreType = METASTORE_IN_MEMORY;
  private String metaStoreJdbcUrl;
  private String metaStoreJdbcUserName;
  private String metaStoreJdbcPassword;
  private int metaStoreJdbcConnectionPoolSize;


  public boolean shouldExportResultsToLog() {
    return exportResultsToLog;
  }

  public void setExportResultsToLog(final boolean exportResultsToLog) {
    this.exportResultsToLog = exportResultsToLog;
  }

  public boolean shouldExportResultsToElasticsearch() {
    return exportResultsToElasticsearch;
  }

  public void setExportResultsToElasticsearch(final boolean exportResultsToElasticsearch) {
    this.exportResultsToElasticsearch = exportResultsToElasticsearch;
  }

  public boolean shouldExportResultsToKinesis() {
    return exportResultsToKinesis;
  }

  public void setExportResultsToKinesis(final boolean exportResultsToKinesis) {
    this.exportResultsToKinesis = exportResultsToKinesis;
  }

  public String getElasticsearchUrl() {
    return elasticsearchUrl;
  }

  public void setElasticsearchUrl(final String elasticsearchUrl) {
    this.elasticsearchUrl = elasticsearchUrl;
  }

  public String getElasticsearchIndexPrefix() {
    return elasticsearchIndexPrefix;
  }

  public void setElasticsearchIndexPrefix(final String elasticsearchIndexPrefix) {
    this.elasticsearchIndexPrefix = elasticsearchIndexPrefix;
  }

  public String getKinesisDataStream() {
    return this.kinesisDataStream;
  }

  public void setKinesisDataStream(final String kinesisDataStream) {
    this.kinesisDataStream = kinesisDataStream;
  }

  public String getKmsType() {
    return kmsType;
  }

  public void setKmsType(final String kmsType) {
    this.kmsType = kmsType;
  }

  public Map<String, String> getKmsAwsRegionMap() {
    return Splitter.on(',').withKeyValueSeparator('=').split(this.kmsAwsRegionMap);
  }

  public void setKmsAwsRegionMap(final String kmsAwsRegionMap) {
    this.kmsAwsRegionMap = kmsAwsRegionMap;
  }

  public String getKmsAwsPreferredRegion() {
    if (kmsAwsPreferredRegion != null) {
      return kmsAwsPreferredRegion;
    }
    else {
      Region region = Regions.getCurrentRegion();
      if (region != null) {
        return region.getName();
      }
    }
    return AWS_DEFAULT_PREFERRED_REGION;
  }

  public void setKmsAwsPreferredRegion(final String kmsAwsPreferredRegion) {
    this.kmsAwsPreferredRegion = kmsAwsPreferredRegion;
  }

  public String getMetaStoreType() {
    return metaStoreType;
  }

  public void setMetaStoreType(final String metaStoreType) {
    this.metaStoreType = metaStoreType;
  }

  public String getMetaStoreJdbcUrl() {
    return this.metaStoreJdbcUrl;
  }

  public void setMetaStoreJdbcUrl(final String metaStoreJdbcUrl) {
    this.metaStoreJdbcUrl = metaStoreJdbcUrl;
  }

  public String getMetaStoreJdbcUserName() {
    return this.metaStoreJdbcUserName;
  }

  public void setMetaStoreJdbcUserName(final String metaStoreJdbcUserName) {
    this.metaStoreJdbcUserName = metaStoreJdbcUserName;
  }

  public String getMetaStoreJdbcPassword() {
    return this.metaStoreJdbcPassword;
  }

  public void setMetaStoreJdbcPassword(final String metaStoreJdbcPassword) {
    this.metaStoreJdbcPassword = metaStoreJdbcPassword;
  }

  public void setMetaStoreJdbcConnectionPoolSize(final int metaStoreJdbcConnectionPoolSize) {
    this.metaStoreJdbcConnectionPoolSize = metaStoreJdbcConnectionPoolSize;
  }

  public int getMetaStoreJdbcConnectionPoolSize() {
    return this.metaStoreJdbcConnectionPoolSize;
  }
}
