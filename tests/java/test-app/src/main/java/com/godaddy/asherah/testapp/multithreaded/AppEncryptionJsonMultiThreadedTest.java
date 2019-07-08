package com.godaddy.asherah.testapp.multithreaded;

import com.godaddy.asherah.appencryption.AppEncryption;
import com.godaddy.asherah.appencryption.AppEncryptionSessionFactory;
import com.godaddy.asherah.testapp.ConfigurationParameterResolver;
import com.godaddy.asherah.testapp.ConfigurationParameterResolver.ConfigurationParameter;
import com.godaddy.asherah.testapp.utils.PayloadGenerator;
import com.godaddy.asherah.testapp.utils.SessionFactoryGenerator;

import org.json.JSONObject;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.time.LocalDateTime;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;

import static com.godaddy.asherah.testapp.testhelpers.Constants.*;
import static org.junit.jupiter.api.Assertions.*;

@ExtendWith(ConfigurationParameterResolver.class)
public class AppEncryptionJsonMultiThreadedTest {
  private static final Logger LOG = LoggerFactory.getLogger(AppEncryptionJsonMultiThreadedTest.class);

  private JSONObject payload;
  private AppEncryptionSessionFactory appEncryptionSessionFactory;
  private String partitionId;
  private AppEncryption<JSONObject, byte[]> appEncryptionJson;

  @BeforeEach
  public void setupTest(@ConfigurationParameter(TEST_PARAM_PAYLOAD_SIZE_BYTES) final int payloadSizeBytes) {
    payload = PayloadGenerator.createRandomJsonPayload(payloadSizeBytes);
    appEncryptionSessionFactory = SessionFactoryGenerator.createDefaultAppEncryptionSessionFactory();
    partitionId  = DEFAULT_PARTITION_ID + "_" + LocalDateTime.now().toString();
    appEncryptionJson = appEncryptionSessionFactory.getAppEncryptionJson(partitionId);
  }

  @AfterEach
  public void tearDown() {
    appEncryptionJson.close();
    appEncryptionSessionFactory.close();
  }

  @Test
  public void appEncryptionEncryptMultipleThreads(@ConfigurationParameter(TEST_PARAM_NUM_THREADS) final int numThreads) {
    List<Future<byte[]>> dataRowRecord = new ArrayList<>();
    LOG.info("Running appEncryptionEncryptMultipleThreads test with {} threads", numThreads);

    ExecutorService service = Executors.newFixedThreadPool(numThreads);

    for (int i = 0; i < numThreads; i++) {
      Future<byte[]> future = service.submit(() -> {
        try {
          byte[] drr = appEncryptionJson.encrypt(payload);

          return drr;
        }
        catch (Exception e) {
          LOG.error("unexpected error during call", e);
          throw e;
        }
      });

      dataRowRecord.add(future);
    }
    service.shutdown();

    for (Future<byte[]> future: dataRowRecord) {
      try {
        byte[] decryptedJsonObject = future.get();
        assertTrue(payload.similar(appEncryptionJson.decrypt(decryptedJsonObject)));
      }
      catch (ExecutionException | InterruptedException e) {
        LOG.error("unexpected error ", e);
        fail("Exception in app encryption library while encrypting: " + e.getMessage());
      }
    }
  }
}
