package com.godaddy.asherah.multithreaded;

import com.godaddy.asherah.TestSetup;
import com.godaddy.asherah.appencryption.Session;
import com.godaddy.asherah.appencryption.SessionFactory;
import com.godaddy.asherah.utils.PayloadGenerator;
import com.godaddy.asherah.utils.SessionFactoryGenerator;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.time.LocalDateTime;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;

import static com.godaddy.asherah.testhelpers.Constants.*;
import static org.junit.jupiter.api.Assertions.*;

class SessionByteMultiThreadedIT {

  private static final Logger LOG = LoggerFactory.getLogger(SessionByteMultiThreadedIT.class);

  private byte[] payload;
  private SessionFactory sessionFactory;
  private String partitionId;
  private Session<byte[], byte[]> sessionBytes;

  @BeforeEach
  public void setupTest() {
    payload = PayloadGenerator.createRandomBytePayload(TEST_PARAM_PAYLOAD_SIZE_BYTES);
    sessionFactory = SessionFactoryGenerator.createDefaultSessionFactory(TestSetup.createKeyManagemementService(),
      TestSetup.createMetastore());
    partitionId = DEFAULT_PARTITION_ID + "_" + LocalDateTime.now().toString();
    sessionBytes = sessionFactory.getSessionBytes(partitionId);
  }

  @AfterEach
  public void tearDown() {
    sessionBytes.close();
    sessionFactory.close();
  }

  @Test
  public void sessionEncryptMultipleThreads() {
    List<Future<byte[]>> dataRowRecord = new ArrayList<>();
    LOG.info("Running sessionEncryptMultipleThreads test with {} threads", TEST_PARAM_NUM_THREADS);

    ExecutorService service = Executors.newFixedThreadPool(TEST_PARAM_NUM_THREADS);

    for (int i = 0; i < TEST_PARAM_NUM_THREADS; i++) {
      Future<byte[]> future = service.submit(() -> {
        try {
          byte[] drr = sessionBytes.encrypt(payload);

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
        byte[] decryptedPayload = future.get();
        assertArrayEquals(payload, sessionBytes.decrypt(decryptedPayload));

      }
      catch (ExecutionException | InterruptedException e) {
        LOG.error("unexpected error ", e);
        fail("Exception in app encryption library while encrypting: " + e.getMessage());
      }
    }
  }
}
