package com.godaddy.asherah.grpc;

import com.godaddy.asherah.appencryption.SessionFactory;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import com.google.protobuf.ByteString;
import io.grpc.inprocess.*;
import io.grpc.ManagedChannel;
import io.grpc.stub.StreamObserver;
import org.junit.jupiter.api.*;
import org.mockito.ArgumentCaptor;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.time.temporal.ChronoUnit;
import java.util.Base64;

import static com.godaddy.asherah.grpc.AppEncryptionProtos.*;
import static com.godaddy.asherah.grpc.AppEncryptionGrpc.*;
import static com.godaddy.asherah.grpc.Constants.*;
import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

class AppEncryptionImplTest {

  SessionFactory sessionFactory;
  final long parentKeyMetaCreatedTime, ekrCreatedTime;
  final String drrBytes, ekrBytes, parentKeyMetaKeyId;

  private AppEncryptionServer appEncryptionServer;
  private ManagedChannel inProcessChannel;

  public AppEncryptionImplTest() {
    parentKeyMetaCreatedTime = Instant.now().getEpochSecond();
    ekrCreatedTime = Instant.now().minus(1, ChronoUnit.DAYS).getEpochSecond();
    parentKeyMetaKeyId = "someId";
    drrBytes = "c29tZVJhbmRvbUJ5dGVzCg=="; // "someRandomBytes" in Base64 encoding
    ekrBytes = "ZWtyQnl0ZXMK"; // "ekrBytes" in Base64 encoding
  }

  @BeforeEach
  void setupTest() throws IOException {
    sessionFactory = SessionFactory
      .newBuilder("product", "service")
      .withInMemoryMetastore()
      .withNeverExpiredCryptoPolicy()
      .withStaticKeyManagementService("test_master_key_that_is_32_bytes")
      .build();

    String serverName = InProcessServerBuilder.generateName();
    appEncryptionServer = new AppEncryptionServer(sessionFactory, "/tmp/testserver.sock",
      InProcessServerBuilder.forName(serverName).directExecutor());
    appEncryptionServer.start();
    inProcessChannel = InProcessChannelBuilder.forName(serverName).directExecutor().build();
  }

  @AfterEach
  void tearDown() throws InterruptedException {
    sessionFactory.close();
    inProcessChannel.shutdown();
    appEncryptionServer.stop();
  }

  @Test
  void testTransformJsonToDrr() {
    AppEncryptionImpl appEncryption = new AppEncryptionImpl(sessionFactory);

    String actualJson =
      "{\"" + DRR_DATA + "\":\"" + drrBytes +
        "\",\"" + DRR_KEY + "\":{\"" + EKR_PARENTKEYMETA + "\":{\"" +
        "" + PARENTKEYMETA_KEYID + "\":\"" + parentKeyMetaKeyId + "\",\"" +
        "" + PARENTKEYMETA_CREATED + "\":" + parentKeyMetaCreatedTime + "}," +
        "\"" + EKR_KEY + "\":\"" + ekrBytes + "\"," +
        "\"" + EKR_CREATED + "\":" + ekrCreatedTime + "}}";

    JsonObject drrJson = new JsonParser().parse(actualJson).getAsJsonObject();
    AppEncryptionProtos.DataRowRecord dataRowRecord = appEncryption.transformJsonToDrr(drrJson);

    assertEquals(dataRowRecord.getData(), ByteString.copyFrom(Base64.getDecoder().decode(drrBytes)));
    assertEquals(dataRowRecord.getKey().getKey(), ByteString.copyFrom(Base64.getDecoder().decode(ekrBytes)));
    assertEquals(dataRowRecord.getKey().getCreated(), ekrCreatedTime);
    assertEquals(dataRowRecord.getKey().getParentKeyMeta().getKeyId(), parentKeyMetaKeyId);
    assertEquals(dataRowRecord.getKey().getParentKeyMeta().getCreated(), parentKeyMetaCreatedTime);
  }

  @Test
  void testTransformDrrToJson() {
    AppEncryptionImpl appEncryption = new AppEncryptionImpl(sessionFactory);

    String expectedJson =
      "{\"" + DRR_DATA + "\":\"" + drrBytes +
        "\",\"" + DRR_KEY + "\":{\"" + EKR_PARENTKEYMETA + "\":{\"" +
        "" + PARENTKEYMETA_KEYID + "\":\"" + parentKeyMetaKeyId + "\",\"" +
        "" + PARENTKEYMETA_CREATED + "\":" + parentKeyMetaCreatedTime + "}," +
        "\"" + EKR_KEY + "\":\"" + ekrBytes + "\"," +
        "\"" + EKR_CREATED + "\":" + ekrCreatedTime + "}}";

    AppEncryptionProtos.DataRowRecord dataRowRecord = AppEncryptionProtos.DataRowRecord.newBuilder()
      .setData(ByteString.copyFrom(Base64.getDecoder().decode(drrBytes)))
      .setKey(AppEncryptionProtos.EnvelopeKeyRecord.newBuilder()
        .setParentKeyMeta(AppEncryptionProtos.KeyMeta.newBuilder()
          .setCreated(parentKeyMetaCreatedTime)
          .setKeyId(parentKeyMetaKeyId)
          .build())
        .setKey(ByteString.copyFrom(Base64.getDecoder().decode(ekrBytes)))
        .setCreated(ekrCreatedTime)
        .build())
      .build();

    JsonObject drrJson = appEncryption.transformDrrToJson(dataRowRecord);
    assertEquals(expectedJson, drrJson.toString());
  }

  @Test
  void testEncryptDecryptRoundTrip() {
    int timesOnNext = 0;
    StreamObserver<SessionResponse> responseObserver = mock(StreamObserver.class);
    AppEncryptionStub appEncryptionStub = AppEncryptionGrpc.newStub(inProcessChannel);

    StreamObserver<SessionRequest> requestObserver = appEncryptionStub.session(responseObserver);
    verify(responseObserver, never()).onNext(any(SessionResponse.class));

    GetSession getSession = GetSession.newBuilder().setPartitionId("partition-1").build();
    requestObserver.onNext(SessionRequest.newBuilder().setGetSession(getSession).build());
    ArgumentCaptor<SessionResponse> sessionResponseArgumentCaptor = ArgumentCaptor.forClass(SessionResponse.class);
    verify(responseObserver, timeout(100).times(++timesOnNext)).onNext(sessionResponseArgumentCaptor.capture());

    // Try to encrypt a payload
    String originalPayloadString = "mysupersecretpayload";
    ByteString bytes = ByteString.copyFrom(originalPayloadString.getBytes(StandardCharsets.UTF_8));
    Encrypt dataToBeEncrypted = Encrypt.newBuilder().setData(bytes).build();
    requestObserver.onNext(SessionRequest.newBuilder().setEncrypt(dataToBeEncrypted).build());
    sessionResponseArgumentCaptor = ArgumentCaptor.forClass(SessionResponse.class);
    verify(responseObserver, timeout(100).times(++timesOnNext)).onNext(sessionResponseArgumentCaptor.capture());
    assertTrue(sessionResponseArgumentCaptor.getValue().hasEncryptResponse());
    DataRowRecord dataRowRecord = sessionResponseArgumentCaptor.getValue().getEncryptResponse().getDataRowRecord();

    // Try to decrypt back the payload
    Decrypt dataToBeDecrypted = Decrypt.newBuilder().setDataRowRecord(dataRowRecord).build();
    requestObserver.onNext(SessionRequest.newBuilder().setDecrypt(dataToBeDecrypted).build());
    sessionResponseArgumentCaptor = ArgumentCaptor.forClass(SessionResponse.class);
    verify(responseObserver, timeout(100).times(++timesOnNext)).onNext(sessionResponseArgumentCaptor.capture());
    assertTrue(sessionResponseArgumentCaptor.getValue().hasDecryptResponse());
    String decryptedPayload = new String(sessionResponseArgumentCaptor.getValue().getDecryptResponse().getData().toByteArray(), StandardCharsets.UTF_8);

    // Verify that both the payloads match
    assertEquals(originalPayloadString, decryptedPayload);

    requestObserver.onCompleted();
    verify(responseObserver, never()).onError(any(Throwable.class));
    verify(responseObserver, timeout(100)).onCompleted();
  }

  @Test
  void testEncryptWithoutGetSessionShouldFail() {
    int timesOnNext = 0;
    StreamObserver<SessionResponse> responseObserver = mock(StreamObserver.class);
    AppEncryptionStub appEncryptionStub = AppEncryptionGrpc.newStub(inProcessChannel);

    StreamObserver<SessionRequest> requestObserver = appEncryptionStub.session(responseObserver);
    verify(responseObserver, never()).onNext(any(SessionResponse.class));

    // Try to encrypt a payload
    String originalPayloadString = "mysupersecretpayload";
    ByteString bytes = ByteString.copyFrom(originalPayloadString.getBytes(StandardCharsets.UTF_8));
    Encrypt dataToBeEncrypted = Encrypt.newBuilder().setData(bytes).build();
    requestObserver.onNext(SessionRequest.newBuilder().setEncrypt(dataToBeEncrypted).build());
    ArgumentCaptor<SessionResponse> sessionResponseArgumentCaptor = ArgumentCaptor.forClass(SessionResponse.class);
    verify(responseObserver, timeout(100).times(++timesOnNext)).onNext(sessionResponseArgumentCaptor.capture());
    assertTrue(sessionResponseArgumentCaptor.getValue().hasErrorResponse());
    String message = sessionResponseArgumentCaptor.getValue().getErrorResponse().getMessage();
    requestObserver.onCompleted();

    assertEquals("a session must be initialized before encrypt", message);
    verify(responseObserver, never()).onError(any(Throwable.class));
    verify(responseObserver, timeout(100)).onCompleted();
  }

  @Test
  void testDecryptWithoutGetSessionShouldFail() {
    int timesOnNext = 0;
    StreamObserver<SessionResponse> responseObserver = mock(StreamObserver.class);
    AppEncryptionStub appEncryptionStub = AppEncryptionGrpc.newStub(inProcessChannel);

    StreamObserver<SessionRequest> requestObserver = appEncryptionStub.session(responseObserver);
    verify(responseObserver, never()).onNext(any(SessionResponse.class));

    // Try to decrypt something
    Decrypt dataToBeDecrypted = Decrypt.newBuilder().setDataRowRecord(DataRowRecord.getDefaultInstance()).build();
    requestObserver.onNext(SessionRequest.newBuilder().setDecrypt(dataToBeDecrypted).build());
    ArgumentCaptor<SessionResponse> sessionResponseArgumentCaptor = ArgumentCaptor.forClass(SessionResponse.class);
    verify(responseObserver, timeout(100).times(++timesOnNext)).onNext(sessionResponseArgumentCaptor.capture());
    assertTrue(sessionResponseArgumentCaptor.getValue().hasErrorResponse());
    String message = sessionResponseArgumentCaptor.getValue().getErrorResponse().getMessage();
    requestObserver.onCompleted();

    assertEquals("a session must be initialized before decrypt", message);
    verify(responseObserver, never()).onError(any(Throwable.class));
    verify(responseObserver, timeout(100)).onCompleted();
  }

  @Test
  void testGetSessionTwiceOnSamePartitionIdShouldGiveErrorResponse() {
    int timesOnNext = 0;
    StreamObserver<SessionResponse> responseObserver = mock(StreamObserver.class);
    AppEncryptionStub appEncryptionStub = AppEncryptionGrpc.newStub(inProcessChannel);

    StreamObserver<SessionRequest> requestObserver = appEncryptionStub.session(responseObserver);
    verify(responseObserver, never()).onNext(any(SessionResponse.class));

    GetSession getSession = GetSession.newBuilder().setPartitionId("partition-1").build();
    requestObserver.onNext(SessionRequest.newBuilder().setGetSession(getSession).build());
    ArgumentCaptor<SessionResponse> sessionResponseArgumentCaptor = ArgumentCaptor.forClass(SessionResponse.class);
    verify(responseObserver, timeout(100).times(++timesOnNext)).onNext(sessionResponseArgumentCaptor.capture());

    // Try to call getSession again
    requestObserver.onNext(SessionRequest.newBuilder().setGetSession(getSession).build());
    sessionResponseArgumentCaptor = ArgumentCaptor.forClass(SessionResponse.class);
    verify(responseObserver, timeout(100).times(++timesOnNext)).onNext(sessionResponseArgumentCaptor.capture());
    assertTrue(sessionResponseArgumentCaptor.getValue().hasErrorResponse());
    String message = sessionResponseArgumentCaptor.getValue().getErrorResponse().getMessage();
    requestObserver.onCompleted();

    assertEquals("session is already initialized", message);
    verify(responseObserver, never()).onError(any(Throwable.class));
    verify(responseObserver, timeout(100)).onCompleted();
  }

  @Test
  void testServerSessionError() {
    StreamObserver<SessionResponse> responseObserver = mock(StreamObserver.class);
    AppEncryptionStub appEncryptionStub = AppEncryptionGrpc.newStub(inProcessChannel);

    StreamObserver<SessionRequest> requestObserver = appEncryptionStub.session(responseObserver);
    GetSession getSession = GetSession.newBuilder().setPartitionId("partition-1").build();
    requestObserver.onNext(SessionRequest.newBuilder().setGetSession(getSession).build());

    // Close the channel midway to stimulate server error
    try {
      inProcessChannel.shutdownNow();
    } catch (Exception ignored) {
    }
    verify(responseObserver).onError(any(Throwable.class));
  }

  @Test
  void testMultipleClients() {
    int timesOnNext = 0;
    StreamObserver<SessionResponse> responseObserver = mock(StreamObserver.class);
    AppEncryptionStub appEncryptionStub = AppEncryptionGrpc.newStub(inProcessChannel);

    // Connect 2 clients to the same server
    StreamObserver<SessionRequest> client1 = appEncryptionStub.session(responseObserver);
    StreamObserver<SessionRequest> client2 = appEncryptionStub.session(responseObserver);
    verify(responseObserver, never()).onNext(any(SessionResponse.class));

    GetSession getSession = GetSession.newBuilder().setPartitionId("partition-1").build();
    client1.onNext(SessionRequest.newBuilder().setGetSession(getSession).build());
    ArgumentCaptor<SessionResponse> sessionResponseArgumentCaptor = ArgumentCaptor.forClass(SessionResponse.class);
    verify(responseObserver, timeout(100).times(++timesOnNext)).onNext(sessionResponseArgumentCaptor.capture());

    getSession = GetSession.newBuilder().setPartitionId("partition-2").build();
    client2.onNext(SessionRequest.newBuilder().setGetSession(getSession).build());
    sessionResponseArgumentCaptor = ArgumentCaptor.forClass(SessionResponse.class);
    verify(responseObserver, timeout(100).times(++timesOnNext)).onNext(sessionResponseArgumentCaptor.capture());

    client1.onCompleted();
    client2.onCompleted();
    verify(responseObserver, never()).onError(any(Throwable.class));
    verify(responseObserver, timeout(100).times(2)).onCompleted();
  }
}
