package com.godaddy.asherah.utils;

import com.godaddy.asherah.TestSetup;
import com.godaddy.asherah.appencryption.SessionFactory;
import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.appencryption.persistence.Metastore;
import org.json.JSONObject;

import static com.godaddy.asherah.testhelpers.Constants.DEFAULT_PRODUCT_ID;
import static com.godaddy.asherah.testhelpers.Constants.DEFAULT_SYSTEM_ID;

public final class SessionFactoryGenerator {

  private SessionFactoryGenerator() {
  }

  public static SessionFactory createDefaultSessionFactory(KeyManagementService keyManagementService,
      Metastore<JSONObject> metastore) {
    return createDefaultSessionFactory(DEFAULT_PRODUCT_ID, DEFAULT_SYSTEM_ID, keyManagementService, metastore);
  }

  public static SessionFactory createDefaultSessionFactory(final String productId, final String serviceId,
      KeyManagementService keyManagementService, Metastore<JSONObject> metastore) {
    return SessionFactory.newBuilder(productId, serviceId)
      .withMetastore(metastore)
      .withNeverExpiredCryptoPolicy()
      .withKeyManagementService(keyManagementService)
      .withMetricsEnabled()
      .build();
  }
}
