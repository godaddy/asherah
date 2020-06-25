package com.godaddy.asherah.grpc;

import com.godaddy.asherah.appencryption.Session;
import com.godaddy.asherah.appencryption.SessionFactory;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import com.google.protobuf.ByteString;
import io.grpc.Status;
import io.grpc.StatusRuntimeException;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.nio.charset.StandardCharsets;
import java.util.Base64;

import io.grpc.stub.StreamObserver;

import static com.godaddy.asherah.grpc.AppEncryptionProtos.SessionRequest;
import static com.godaddy.asherah.grpc.AppEncryptionProtos.SessionResponse;
import static com.godaddy.asherah.grpc.AppEncryptionProtos.DataRowRecord;
import static com.godaddy.asherah.grpc.AppEncryptionProtos.EnvelopeKeyRecord;
import static com.godaddy.asherah.grpc.AppEncryptionProtos.KeyMeta;
import static com.godaddy.asherah.grpc.AppEncryptionProtos.EncryptResponse;
import static com.godaddy.asherah.grpc.AppEncryptionProtos.DecryptResponse;
import static com.godaddy.asherah.grpc.AppEncryptionProtos.ErrorResponse;

public class AppEncryptionImpl extends AppEncryptionGrpc.AppEncryptionImplBase {
  private static final Logger logger = LoggerFactory.getLogger(AppEncryptionImpl.class);

  private final SessionFactory sessionFactory;

  /**
   * Constructor for AppEncryptionImpl.
   *
   * @param sessionFactory The {@link SessionFactory} to use for the server session
   */
  public AppEncryptionImpl(final SessionFactory sessionFactory) {
    this.sessionFactory = sessionFactory;
  }

  @Override
  public StreamObserver<SessionRequest> session(final StreamObserver<SessionResponse> responseObserver) {
    logger.info("connecting stream observer");

    StreamObserver<SessionRequest> streamObserver = new StreamObserver<SessionRequest>() {

      private Session<byte[], byte[]> sessionBytes;
      private String partitionId;

      @Override
      public void onNext(final SessionRequest sessionRequest) {
        logger.debug("sessionRequest={}", sessionRequest);

        if (sessionRequest.hasGetSession()) {
          // Handle response for get session
          if (sessionBytes != null) {
            sendErrorResponse("session is already initialized", responseObserver);
            return;
          }
          partitionId = sessionRequest.getGetSession().getPartitionId();
          logger.info("attempting to create session for partitionId={}", partitionId);
          sessionBytes = sessionFactory.getSessionBytes(partitionId);
          responseObserver.onNext(SessionResponse.getDefaultInstance());
        }

        if (sessionRequest.hasEncrypt()) {
          if (sessionBytes == null) {
            sendErrorResponse("a session must be initialized before encrypt", responseObserver);
            return;
          }
          // handle response for encrypt
          logger.info("handling encrypt for partitionId={}", partitionId);
          String payloadString = sessionRequest.getEncrypt().getData().toStringUtf8();
          byte[] dataRowRecordBytes = sessionBytes.encrypt(payloadString.getBytes(StandardCharsets.UTF_8));
          String drr = new String(dataRowRecordBytes, StandardCharsets.UTF_8);

          DataRowRecord dataRowRecordValue = transformJsonToDrr(new JsonParser().parse(drr).getAsJsonObject());

          EncryptResponse encryptResponse = EncryptResponse.newBuilder().setDataRowRecord(dataRowRecordValue).build();
          responseObserver.onNext(SessionResponse.newBuilder().setEncryptResponse(encryptResponse).build());
        }

        if (sessionRequest.hasDecrypt()) {
          if (sessionBytes == null) {
            sendErrorResponse("a session must be initialized before decrypt", responseObserver);
            return;
          }
          // handle response for decrypt
          logger.info("handling decrypt for partitionId={}", partitionId);
          DataRowRecord dataRowRecord = sessionRequest.getDecrypt().getDataRowRecord();

          JsonObject drrJson = transformDrrToJson(dataRowRecord);

          byte[] dataRowRecordBytes = drrJson.toString().getBytes(StandardCharsets.UTF_8);
          byte[] decryptedBytes = sessionBytes.decrypt(dataRowRecordBytes);
          DecryptResponse decryptResponse =
              DecryptResponse.newBuilder().setData(ByteString.copyFrom(decryptedBytes)).build();
          responseObserver.onNext(SessionResponse.newBuilder().setDecryptResponse(decryptResponse).build());
        }
      }

      @Override
      public void onError(final Throwable throwable) {
        // Client may send a half close request
        // https://github.com/grpc/grpc-java/issues/6560
        if (((StatusRuntimeException) throwable).getStatus().getCode() == Status.Code.CANCELLED) {
          logger.info("session completed for partitionId={}", partitionId);
          if (sessionBytes != null) {
            sessionBytes.close();
          }
          logger.debug("client terminated", throwable);
          return;
        }
        logger.error("server session error for partitionId={}", partitionId);
        responseObserver.onError(throwable);
        throwable.printStackTrace();
      }

      @Override
      public void onCompleted() {
        logger.info("session completed for partitionId={}", partitionId);
        responseObserver.onCompleted();
        if (sessionBytes != null) {
          sessionBytes.close();
        }
      }
    };

    return streamObserver;
  }

  private void sendErrorResponse(final String errorMessage, final StreamObserver<SessionResponse> responseObserver) {
    ErrorResponse errorResponse = AppEncryptionProtos
        .ErrorResponse.newBuilder().setMessage(errorMessage).build();
    responseObserver.onNext(SessionResponse.newBuilder().setErrorResponse(errorResponse).build());
  }

  JsonObject transformDrrToJson(final DataRowRecord dataRowRecord) {

    // Build ParentKeyMeta Json
    JsonObject parentKeyMetaJson = new JsonObject();
    parentKeyMetaJson.addProperty(Constants.PARENTKEYMETA_KEYID, dataRowRecord.getKey().getParentKeyMeta().getKeyId());
    parentKeyMetaJson.addProperty(
        Constants.PARENTKEYMETA_CREATED, dataRowRecord.getKey().getParentKeyMeta().getCreated());

    // Build EKR Json
    JsonObject ekrJson = new JsonObject();
    ekrJson.add(Constants.EKR_PARENTKEYMETA, parentKeyMetaJson);
    ekrJson.addProperty(
        Constants.EKR_KEY, Base64.getEncoder().encodeToString(dataRowRecord.getKey().getKey().toByteArray()));
    ekrJson.addProperty(Constants.EKR_CREATED, dataRowRecord.getKey().getCreated());

    // Build DRR Json
    JsonObject drrJson = new JsonObject();
    drrJson.addProperty(Constants.DRR_DATA, Base64.getEncoder().encodeToString(dataRowRecord.getData().toByteArray()));
    drrJson.add(Constants.DRR_KEY, ekrJson);

    return drrJson;
  }

  DataRowRecord transformJsonToDrr(final JsonObject drrJson) {

    JsonObject ekrJson = (JsonObject) drrJson.get(Constants.DRR_KEY);
    JsonObject parentKeyMetaJson = (JsonObject) ekrJson.get(Constants.EKR_PARENTKEYMETA);

    // Build ParentKeyMeta value
    String parentKeyMetaKeyId = parentKeyMetaJson.get(Constants.PARENTKEYMETA_KEYID).getAsString();
    long parentKeyMetaCreated = parentKeyMetaJson.get(Constants.PARENTKEYMETA_CREATED).getAsLong();
    KeyMeta keyMetaValue = KeyMeta.newBuilder()
        .setCreated(parentKeyMetaCreated)
        .setKeyId(parentKeyMetaKeyId)
        .build();

    // Build EKR value
    String ekrKeyString = ekrJson.get(Constants.EKR_KEY).getAsString();
    byte[] ekrKeyBytes = Base64.getDecoder().decode(ekrKeyString);
    long ekrCreatedValue = ekrJson.get(Constants.EKR_CREATED).getAsLong();
    EnvelopeKeyRecord envelopeKeyRecordValue = EnvelopeKeyRecord.newBuilder()
        .setCreated(ekrCreatedValue)
        .setKey(ByteString.copyFrom(ekrKeyBytes))
        .setParentKeyMeta(keyMetaValue)
        .build();

    // Build DRR value
    String drrDataString = drrJson.get(Constants.DRR_DATA).getAsString();
    byte[] drrDataBytes = Base64.getDecoder().decode(drrDataString);
    return DataRowRecord.newBuilder()
        .setData(ByteString.copyFrom(drrDataBytes))
        .setKey(envelopeKeyRecordValue)
        .build();
  }
}
