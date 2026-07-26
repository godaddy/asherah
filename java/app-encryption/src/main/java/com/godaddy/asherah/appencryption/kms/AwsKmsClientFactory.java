package com.godaddy.asherah.appencryption.kms;

import software.amazon.awssdk.regions.Region;
import software.amazon.awssdk.services.kms.KmsClient;

/**
 * A factory to create an AWS KMS client based on the region provided.
 */
public interface AwsKmsClientFactory {

  /**
   * Builds a KMS client for the specified AWS region.
   *
   * @param region the AWS region identifier
   * @return a configured KmsClient instance
   */
  KmsClient build(String region);

  /**
   * Returns the default factory implementation that creates standard KMS clients.
   *
   * @return the default KMS client factory
   */
  static DefaultAwsKmsClientFactory defaultFactory() {
    return new DefaultAwsKmsClientFactory();
  }

  /**
   * Default implementation of {@link AwsKmsClientFactory}.
   */
  class DefaultAwsKmsClientFactory implements AwsKmsClientFactory {

    @Override
    public KmsClient build(final String region) {
      return KmsClient.builder()
        .region(Region.of(region))
        .build();
    }
  }
}
