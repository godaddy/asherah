package com.godaddy.asherah;

import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.appencryption.kms.StaticKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.persistence.InMemoryMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.Metastore;
import org.json.JSONObject;

import static com.godaddy.asherah.testhelpers.Constants.KEY_MANAGEMENT_STATIC_MASTER_KEY;

public class TestSetup {
  public static KeyManagementService getDefaultKeyManagemementService() {
    return new StaticKeyManagementServiceImpl(KEY_MANAGEMENT_STATIC_MASTER_KEY);
  }

  public static Metastore<JSONObject> getDefaultMetastore() {
    return new InMemoryMetastoreImpl<>();
  }
}
