package com.godaddy.asherah.grpc;

import java.nio.charset.StandardCharsets;
import java.util.Base64;

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
import com.google.gson.JsonObject;
import com.google.protobuf.ByteString;
import com.zaxxer.hikari.HikariDataSource;

import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import io.grpc.stub.StreamObserver;
import picocli.CommandLine;

import static com.godaddy.asherah.grpc.AppEncryptionProtos.*;

public class AppEncryptionImpl extends AppEncryptionGrpc.AppEncryptionImplBase {

  private static final Logger logger = LoggerFactory.getLogger(AppEncryptionImpl.class);

  SessionFactory sessionFactory;
  Session<byte[], byte[]> sessionBytes;

  public AppEncryptionImpl() {
    Metastore<JSONObject> metastore = setupMetastore(AppEncryptionServer.metastoreType);
    KeyManagementService keyManagementService = setupKeyManagementService(AppEncryptionServer.kmsType);
    CryptoPolicy cryptoPolicy = setupCryptoPolicy(AppEncryptionServer.keyExpirationDays, AppEncryptionServer.revokeCheckMinutes);

    sessionFactory = SessionFactory.newBuilder(AppEncryptionServer.productId, AppEncryptionServer.serviceId)
      .withMetastore(metastore)
      .withCryptoPolicy(cryptoPolicy)
      .withKeyManagementService(keyManagementService)
      .build();
  }

  @Override
  public StreamObserver<SessionRequest> session(final StreamObserver<SessionResponse> responseObserver) {

    System.out.println("Connecting stream observer");

    StreamObserver<SessionRequest> streamObserver = new StreamObserver<SessionRequest>() {

      @Override
      public void onNext(SessionRequest sessionRequest) {
        System.out.println("onNext from server");
        System.out.println("sessionRequest = " + sessionRequest);

        if (sessionRequest.hasGetSession()) {

          // Handle here for get session
          String partitionId = sessionRequest.getGetSession().getPartitionId();
          sessionBytes = sessionFactory.getSessionBytes(partitionId);
          responseObserver.onNext(SessionResponse.getDefaultInstance());
        }

        if (sessionBytes == null) {
          onError(new Exception("Please initialize a session first"));
          return;
        }

        if (sessionRequest.hasEncrypt()) {

          // handle here for encrypt
          String payloadString = sessionRequest.getEncrypt().getData().toStringUtf8();
          byte[] dataRowRecordBytes = sessionBytes.encrypt(payloadString.getBytes(StandardCharsets.UTF_8));
          String drr = new String(dataRowRecordBytes, StandardCharsets.UTF_8);
          System.out.println("drr = " + drr);

          DataRowRecord dataRowRecordValue = tranformJsonToDataRowRecord(new JSONObject(drr));

          EncryptResponse encryptResponse = EncryptResponse.newBuilder().setDataRowRecord(dataRowRecordValue).build();
          responseObserver.onNext(SessionResponse.newBuilder().setEncryptResponse(encryptResponse).build());
        }

        if (sessionRequest.hasDecrypt()) {

          // handle here for decrypt
          DataRowRecord dataRowRecord = sessionRequest.getDecrypt().getDataRowRecord();

          JsonObject parentKeyMetaJson = new JsonObject();
          parentKeyMetaJson.addProperty("KeyId", dataRowRecord.getKey().getParentKeyMeta().getKeyId());
          parentKeyMetaJson.addProperty("Created", dataRowRecord.getKey().getParentKeyMeta().getCreated());

          JsonObject keyJson = new JsonObject();
          keyJson.add("ParentKeyMeta", parentKeyMetaJson);
          keyJson.addProperty("Key", dataRowRecord.getKey().getKey().toStringUtf8());
          keyJson.addProperty("Created", dataRowRecord.getKey().getCreated());

          JsonObject drrJson = new JsonObject();
          drrJson.addProperty("Data", dataRowRecord.getData().toStringUtf8());
          drrJson.add("Key", keyJson);

          byte[] dataRowRecordBytes = drrJson.toString().getBytes(StandardCharsets.UTF_8);
          byte[] decryptedBytes = sessionBytes.decrypt(dataRowRecordBytes);
          DecryptResponse decryptResponse = DecryptResponse.newBuilder().setData(ByteString.copyFrom(decryptedBytes)).build();
          responseObserver.onNext(SessionResponse.newBuilder().setDecryptResponse(decryptResponse).build());
        }
      }

      @Override
      public void onError(Throwable throwable) {
        System.out.println("on error");
        throwable.printStackTrace();
      }

      @Override
      public void onCompleted() {
        sessionBytes.close();
        sessionFactory.close();
        System.out.println("on completed");
      }
    };

    return streamObserver;
  }

  private DataRowRecord tranformJsonToDataRowRecord(JSONObject drrJson) {

    byte[] drrDataBytes = drrJson.get("Data").toString().getBytes(StandardCharsets.UTF_8);

    JSONObject envelopeKeyRecordJson = (JSONObject) drrJson.get("Key");
    long ekrCreatedValue = Long.parseLong(String.valueOf(envelopeKeyRecordJson.get("Created")));
    byte[] ekrKey = envelopeKeyRecordJson.get("Key").toString().getBytes(StandardCharsets.UTF_8);

    JSONObject parentKeyMetaJson = (JSONObject) envelopeKeyRecordJson.get("ParentKeyMeta");
    String parentKeyMetaKeyId = (String) parentKeyMetaJson.get("KeyId");
    long parentKeyMetaCreated = Long.parseLong(String.valueOf(parentKeyMetaJson.get("Created")));

    KeyMeta keyMetaValue = KeyMeta.newBuilder()
      .setCreated(parentKeyMetaCreated)
      .setKeyId(parentKeyMetaKeyId)
      .build();

    EnvelopeKeyRecord envelopeKeyRecordValue = EnvelopeKeyRecord.newBuilder()
      .setCreated(ekrCreatedValue)
      .setKey(ByteString.copyFrom(ekrKey))
      .setParentKeyMeta(keyMetaValue)
      .build();

    DataRowRecord dataRowRecordValue = DataRowRecord.newBuilder()
      .setData(ByteString.copyFrom(drrDataBytes))
      .setKey(envelopeKeyRecordValue)
      .build();

    return dataRowRecordValue;
  }

  private CryptoPolicy setupCryptoPolicy(int keyExpirationDays, int revokeCheckMinutes) {
    CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy
      .newBuilder()
      .withKeyExpirationDays(keyExpirationDays)
      .withRevokeCheckMinutes(revokeCheckMinutes)
      .withCanCacheSessions(AppEncryptionServer.sessionCacheEnabled)
      .withSessionCacheMaxSize(AppEncryptionServer.sessionCacheMaxSize)
      .withSessionCacheExpireMinutes(AppEncryptionServer.sessionCacheExpireMinutes)
      .build();

    return cryptoPolicy;
  }

  private KeyManagementService setupKeyManagementService(AppEncryptionServer.KmsType kmsType) {
    KeyManagementService keyManagementService;
    if (kmsType == AppEncryptionServer.KmsType.AWS) {
      if (AppEncryptionServer.preferredRegion != null && AppEncryptionServer.regionMap != null) {
        logger.info("using AWS KMS...");

        // build the ARN regions including preferred region
        keyManagementService = AwsKeyManagementServiceImpl.newBuilder(AppEncryptionServer.regionMap, AppEncryptionServer.preferredRegion).build();
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

  private Metastore<JSONObject> setupMetastore(AppEncryptionServer.MetastoreType metastoreType) {
    Metastore<JSONObject> metastore;
    if (metastoreType == AppEncryptionServer.MetastoreType.JDBC) {
      if (AppEncryptionServer.jdbcUrl != null) {
        logger.info("using JDBC-based metastore...");

        // Setup JDBC persistence from command line argument using Hikari connection pooling
        HikariDataSource dataSource = new HikariDataSource();
        dataSource.setJdbcUrl(AppEncryptionServer.jdbcUrl);
        metastore = JdbcMetastoreImpl.newBuilder(dataSource).build();
      } else {
        CommandLine.usage(this, System.out);
        return null;
      }
    } else if (metastoreType == AppEncryptionServer.MetastoreType.DYNAMODB) {
      logger.info("using DynamoDB-based metastore...");

      metastore = DynamoDbMetastoreImpl.newBuilder().build();
    } else {
      logger.info("using in-memory metastore...");

      metastore = new InMemoryMetastoreImpl<>();
    }

    return metastore;
  }
}
