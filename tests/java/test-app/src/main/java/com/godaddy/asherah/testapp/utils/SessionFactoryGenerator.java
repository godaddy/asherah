package com.godaddy.asherah.testapp.utils;

import static com.godaddy.asherah.testapp.testhelpers.Constants.DEFAULT_PRODUCT_ID;
import static com.godaddy.asherah.testapp.testhelpers.Constants.DEFAULT_SYSTEM_ID;

import com.godaddy.asherah.appencryption.AppEncryptionSessionFactory;
import com.godaddy.asherah.testapp.TestSetup;

public final class SessionFactoryGenerator {

  private SessionFactoryGenerator() {

  }

  public static AppEncryptionSessionFactory createDefaultAppEncryptionSessionFactory() {
    return createDefaultAppEncryptionSessionFactory(DEFAULT_PRODUCT_ID, DEFAULT_SYSTEM_ID);
  }

  public static AppEncryptionSessionFactory createDefaultAppEncryptionSessionFactory(final String productId,
                                                                                     final String systemId) {
    // Read volatile first to force other members to be read from memory
    if (!TestSetup.isInitialized()) {
      throw new IllegalStateException("initialization has not been run yet!");
    }

    return AppEncryptionSessionFactory.newBuilder(productId, systemId)
        .withMetastorePersistence(TestSetup.getMetastorePersistence())
        .withNeverExpiredCryptoPolicy()
        .withKeyManagementService(TestSetup.getKeyManagementService())
        .withMetricsEnabled()
        .build();
  }
}
