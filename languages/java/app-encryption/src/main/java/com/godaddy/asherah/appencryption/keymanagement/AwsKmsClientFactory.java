package com.godaddy.asherah.appencryption.keymanagement;

import com.amazonaws.services.kms.AWSKMS;
import com.amazonaws.services.kms.AWSKMSClientBuilder;

class AwsKmsClientFactory {
  AWSKMS createAwsKmsClient(final String region) {
    return AWSKMSClientBuilder.standard()
        .withRegion(region)
        .build();
  }
}
