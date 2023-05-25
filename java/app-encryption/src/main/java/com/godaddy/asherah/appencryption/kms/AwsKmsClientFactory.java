package com.godaddy.asherah.appencryption.kms;

import software.amazon.awssdk.regions.Region;
import software.amazon.awssdk.services.kms.KmsClient;

/**
 * A factory to create an AWS KMS client based on the region provided.
 */
interface AwsKmsClientFactory {

  KmsClient build(String region);

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
