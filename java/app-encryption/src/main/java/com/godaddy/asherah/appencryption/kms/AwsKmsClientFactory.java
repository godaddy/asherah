package com.godaddy.asherah.appencryption.kms;

import com.amazonaws.services.kms.AWSKMS;
import com.amazonaws.services.kms.AWSKMSClientBuilder;

/**
 * A factory to create an AWS KMS client based on the region provided
 */
class AwsKmsClientFactory {
  AWSKMS createAwsKmsClient(final String region) {
    return AWSKMSClientBuilder.standard()
        .withRegion(region)
        .build();
  }
}
