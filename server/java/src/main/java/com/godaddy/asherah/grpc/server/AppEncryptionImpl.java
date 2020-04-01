package com.godaddy.asherah.grpc.server;

import java.nio.charset.StandardCharsets;

import com.godaddy.asherah.App;
import com.godaddy.asherah.appencryption.Session;
import com.godaddy.asherah.appencryption.SessionFactory;
import com.godaddy.asherah.appencryption.kms.AwsKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.appencryption.kms.StaticKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.persistence.DynamoDbMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.InMemoryMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.JdbcMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.Metastore;
import com.godaddy.asherah.crypto.BasicExpiringCryptoPolicy;
import com.godaddy.asherah.crypto.CryptoPolicy;
import com.google.protobuf.ByteString;
import com.zaxxer.hikari.HikariDataSource;

import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import io.grpc.stub.StreamObserver;
import picocli.CommandLine;

import static com.godaddy.asherah.grpc.server.AppEncryptionServer.*;

public class AppEncryptionImpl extends AppEncryptionGrpc.AppEncryptionImplBase {

  private static final Logger logger = LoggerFactory.getLogger(App.class);

  SessionFactory sessionFactory;
  Session<byte[], byte[]> sessionBytes;

  public AppEncryptionImpl() {
    Metastore<JSONObject> metastore = setupMetastore(metastoreType);
    KeyManagementService keyManagementService = setupKeyManagementService(kmsType);
    CryptoPolicy cryptoPolicy = setupCryptoPolicy(keyExpirationDays, revokeCheckMinutes);

    sessionFactory = SessionFactory.newBuilder(productId, serviceId)
      .withMetastore(metastore)
      .withCryptoPolicy(cryptoPolicy)
      .withKeyManagementService(keyManagementService)
      .build();
  }

  @Override
  public StreamObserver<Appencryption.SessionRequest> session(final StreamObserver<Appencryption.SessionResponse> responseObserver) {

    System.out.println("Connecting stream observer");

    StreamObserver<Appencryption.SessionRequest> streamObserver = new StreamObserver<Appencryption.SessionRequest>() {

      @Override
      public void onNext(Appencryption.SessionRequest sessionRequest) {
        System.out.println("onNext from server");
        System.out.println("sessionRequest = " + sessionRequest);

        if (sessionRequest.hasGetSession()) {

          // Handle here for get session
          String partitionId = sessionRequest.getGetSession().getPartitionId();
          sessionBytes = sessionFactory.getSessionBytes(partitionId);
          responseObserver.onNext(Appencryption.SessionResponse.getDefaultInstance());
        }

        if (sessionRequest.hasEncrypt()) {

          // handle here for encrypt
          String payloadString = sessionRequest.getEncrypt().getData().toStringUtf8();
          byte[] dataRowRecordBytes = sessionBytes.encrypt(payloadString.getBytes(StandardCharsets.UTF_8));
          String drr = new String(dataRowRecordBytes, StandardCharsets.UTF_8);

          JSONObject drrJson = new JSONObject(drr);
          byte[] drrDataBytes = drrJson.get("Data").toString().getBytes(StandardCharsets.UTF_8);

          JSONObject envelopeKeyRecordJson = (JSONObject) drrJson.get("Key");
          long ekrCreatedValue = Long.parseLong(String.valueOf(envelopeKeyRecordJson.get("Created")));
          byte[] ekrKey = envelopeKeyRecordJson.get("Key").toString().getBytes(StandardCharsets.UTF_8);

          JSONObject parentKeyMetaJson = (JSONObject) envelopeKeyRecordJson.get("ParentKeyMeta");
          String parentKeyMetaKeyId = (String) parentKeyMetaJson.get("KeyId");
          long parentKeyMetaCreated = Long.parseLong(String.valueOf(parentKeyMetaJson.get("Created")));

          Appencryption.KeyMeta keyMetaValue = Appencryption.KeyMeta.newBuilder()
            .setCreated(parentKeyMetaCreated)
            .setKeyId(parentKeyMetaKeyId)
            .build();

          Appencryption.EnvelopeKeyRecord envelopeKeyRecordValue = Appencryption.EnvelopeKeyRecord.newBuilder()
            .setCreated(ekrCreatedValue)
            .setKey(ByteString.copyFrom(ekrKey))
            .setParentKeyMeta(keyMetaValue)
            .build();

          Appencryption.DataRowRecord dataRowRecordValue = Appencryption.DataRowRecord.newBuilder()
            .setData(ByteString.copyFrom(drrDataBytes))
            .setKey(envelopeKeyRecordValue)
            .build();

          Appencryption.EncryptResponse encryptResponse = Appencryption.EncryptResponse.newBuilder().setDataRowRecord(dataRowRecordValue).build();
          responseObserver.onNext(Appencryption.SessionResponse.newBuilder().setEncryptResponse(encryptResponse).build());
        }

        if (sessionRequest.hasDecrypt()) {
          // handle here for decrypt
        }

        responseObserver.onNext(Appencryption.SessionResponse.getDefaultInstance());
      }

      @Override
      public void onError(Throwable throwable) {
        System.out.println("on error");
        throwable.printStackTrace();
      }

      @Override
      public void onCompleted() {
        System.out.println("on completed");
      }
    };

    return streamObserver;
  }

  private CryptoPolicy setupCryptoPolicy(int keyExpirationDays, int revokeCheckMinutes) {
    CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy
      .newBuilder()
      .withKeyExpirationDays(keyExpirationDays)
      .withRevokeCheckMinutes(revokeCheckMinutes)
      .withCanCacheSessions(sessionCacheEnabled)
      .withSessionCacheMaxSize(sessionCacheMaxSize)
      .withSessionCacheExpireMinutes(sessionCacheExpireMinutes)
      .build();

    return cryptoPolicy;
  }

  private KeyManagementService setupKeyManagementService(KmsType kmsType) {
    KeyManagementService keyManagementService;
    if (kmsType == KmsType.AWS) {
      if (preferredRegion != null && regionMap != null) {
        logger.info("using AWS KMS...");

        // build the ARN regions including preferred region
        keyManagementService = AwsKeyManagementServiceImpl.newBuilder(regionMap, preferredRegion).build();
      } else {
        CommandLine.usage(this, System.out);
        return null;
      }
    } else {
      logger.info("using static KMS...");

      keyManagementService = new StaticKeyManagementServiceImpl("mysupersecretstaticmasterkey!!!!");
    }

    return keyManagementService;
  }

  private Metastore<JSONObject> setupMetastore(MetastoreType metastoreType) {
    Metastore<JSONObject> metastore;
    if (metastoreType == MetastoreType.JDBC) {
      if (jdbcUrl != null) {
        logger.info("using JDBC-based metastore...");

        // Setup JDBC persistence from command line argument using Hikari connection pooling
        HikariDataSource dataSource = new HikariDataSource();
        dataSource.setJdbcUrl(jdbcUrl);
        metastore = JdbcMetastoreImpl.newBuilder(dataSource).build();
      } else {
        CommandLine.usage(this, System.out);
        return null;
      }
    } else if (metastoreType == MetastoreType.DYNAMODB) {
      logger.info("using DynamoDB-based metastore...");

      metastore = DynamoDbMetastoreImpl.newBuilder().build();
    } else {
      logger.info("using in-memory metastore...");

      metastore = new InMemoryMetastoreImpl<>();
    }

    return metastore;
  }
}
