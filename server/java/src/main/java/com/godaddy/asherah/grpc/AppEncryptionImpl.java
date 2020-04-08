package com.godaddy.asherah.grpc;

import java.nio.charset.StandardCharsets;

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
import com.google.gson.JsonParser;
import com.google.protobuf.ByteString;
import com.zaxxer.hikari.HikariDataSource;

import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import io.grpc.stub.StreamObserver;
import picocli.CommandLine;

import static com.godaddy.asherah.grpc.AppEncryptionProtos.*;
import static com.godaddy.asherah.grpc.Constants.*;

public class AppEncryptionImpl extends AppEncryptionGrpc.AppEncryptionImplBase {

  private static final Logger logger = LoggerFactory.getLogger(AppEncryptionImpl.class);

  Metastore<JSONObject> metastore;
  KeyManagementService keyManagementService;
  CryptoPolicy cryptoPolicy;

  public AppEncryptionImpl() {
    metastore = setupMetastore(AppEncryptionServer.metastoreType);
    keyManagementService = setupKeyManagementService(AppEncryptionServer.kmsType);
    cryptoPolicy = setupCryptoPolicy(AppEncryptionServer.keyExpirationDays, AppEncryptionServer.revokeCheckMinutes);
  }

  @Override
  public StreamObserver<SessionRequest> session(final StreamObserver<SessionResponse> responseObserver) {

    System.out.println("Connecting stream observer");

    StreamObserver<SessionRequest> streamObserver = new StreamObserver<SessionRequest>() {

      SessionFactory sessionFactory;
      Session<byte[], byte[]> sessionBytes;

      @Override
      public void onNext(SessionRequest sessionRequest) {
        // For debug purposes
        // System.out.println("sessionRequest = " + sessionRequest);

        if (sessionRequest.hasGetSession()) {
          // Handle response for get session
          sessionFactory = SessionFactory.newBuilder(AppEncryptionServer.productId, AppEncryptionServer.serviceId)
            .withMetastore(metastore)
            .withCryptoPolicy(cryptoPolicy)
            .withKeyManagementService(keyManagementService)
            .build();

          String partitionId = sessionRequest.getGetSession().getPartitionId();
          sessionBytes = sessionFactory.getSessionBytes(partitionId);
          responseObserver.onNext(SessionResponse.getDefaultInstance());
        }

        if (sessionBytes == null) {
          responseObserver.onError(new Exception("Please initialize a session first"));
          return;
        }

        if (sessionRequest.hasEncrypt()) {
          // handle response for encrypt
          String payloadString = sessionRequest.getEncrypt().getData().toStringUtf8();
          byte[] dataRowRecordBytes = sessionBytes.encrypt(payloadString.getBytes(StandardCharsets.UTF_8));
          String drr = new String(dataRowRecordBytes, StandardCharsets.UTF_8);

          DataRowRecord dataRowRecordValue = transformJsonToDrr(new JsonParser().parse(drr).getAsJsonObject());

          EncryptResponse encryptResponse = EncryptResponse.newBuilder().setDataRowRecord(dataRowRecordValue).build();
          responseObserver.onNext(SessionResponse.newBuilder().setEncryptResponse(encryptResponse).build());
        }

        if (sessionRequest.hasDecrypt()) {
          // handle response for decrypt
          DataRowRecord dataRowRecord = sessionRequest.getDecrypt().getDataRowRecord();

          JsonObject drrJson = transformDrrToJson(dataRowRecord);

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

  JsonObject transformDrrToJson(DataRowRecord dataRowRecord) {

    // Build ParentKeyMeta Json
    JsonObject parentKeyMetaJson = new JsonObject();
    parentKeyMetaJson.addProperty(PARENTKEYMETA_KEYID, dataRowRecord.getKey().getParentKeyMeta().getKeyId());
    parentKeyMetaJson.addProperty(PARENTKEYMETA_CREATED, dataRowRecord.getKey().getParentKeyMeta().getCreated());

    // Build EKR Json
    JsonObject ekrJson = new JsonObject();
    ekrJson.add(EKR_PARENTKEYMETA, parentKeyMetaJson);
    ekrJson.addProperty(EKR_KEY, dataRowRecord.getKey().getKey().toStringUtf8());
    ekrJson.addProperty(EKR_CREATED, dataRowRecord.getKey().getCreated());

    // Build DRR Json
    JsonObject drrJson = new JsonObject();
    drrJson.addProperty(DRR_DATA, dataRowRecord.getData().toStringUtf8());
    drrJson.add(DRR_KEY, ekrJson);

    return drrJson;
  }

  DataRowRecord transformJsonToDrr(JsonObject drrJson) {

    JsonObject ekrJson = (JsonObject) drrJson.get(DRR_KEY);
    JsonObject parentKeyMetaJson = (JsonObject) ekrJson.get(EKR_PARENTKEYMETA);

    // Build ParentKeyMeta value
    String parentKeyMetaKeyId = parentKeyMetaJson.get(PARENTKEYMETA_KEYID).getAsString();
    long parentKeyMetaCreated = parentKeyMetaJson.get(PARENTKEYMETA_CREATED).getAsLong();
    KeyMeta keyMetaValue = KeyMeta.newBuilder()
      .setCreated(parentKeyMetaCreated)
      .setKeyId(parentKeyMetaKeyId)
      .build();

    // Build EKR value
    byte[] ekrKey = ekrJson.get(EKR_KEY).getAsString().getBytes(StandardCharsets.UTF_8);
    long ekrCreatedValue = ekrJson.get(EKR_CREATED).getAsLong();
    EnvelopeKeyRecord envelopeKeyRecordValue = EnvelopeKeyRecord.newBuilder()
      .setCreated(ekrCreatedValue)
      .setKey(ByteString.copyFrom(ekrKey))
      .setParentKeyMeta(keyMetaValue)
      .build();

    // Build DRR value
    byte[] drrDataBytes = drrJson.get(DRR_DATA).getAsString().getBytes(StandardCharsets.UTF_8);
    return DataRowRecord.newBuilder()
      .setData(ByteString.copyFrom(drrDataBytes))
      .setKey(envelopeKeyRecordValue)
      .build();
  }

  CryptoPolicy setupCryptoPolicy(int keyExpirationDays, int revokeCheckMinutes) {

    return BasicExpiringCryptoPolicy
      .newBuilder()
      .withKeyExpirationDays(keyExpirationDays)
      .withRevokeCheckMinutes(revokeCheckMinutes)
      .withCanCacheSessions(AppEncryptionServer.sessionCacheEnabled)
      .withSessionCacheMaxSize(AppEncryptionServer.sessionCacheMaxSize)
      .withSessionCacheExpireMinutes(AppEncryptionServer.sessionCacheExpireMinutes)
      .build();
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
