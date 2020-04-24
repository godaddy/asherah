package com.godaddy.asherah.grpc;

import com.godaddy.asherah.appencryption.SessionFactory;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import com.google.protobuf.ByteString;
import io.grpc.inprocess.InProcessChannelBuilder;
import io.grpc.inprocess.InProcessServerBuilder;
import io.grpc.stub.StreamObserver;
import org.junit.jupiter.api.*;

import java.io.IOException;
import java.lang.reflect.Field;
import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.time.temporal.ChronoUnit;
import java.util.List;
import java.util.Map;
import java.util.concurrent.atomic.AtomicReference;

import io.grpc.ManagedChannel;
import picocli.CommandLine;

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
    drrBytes = "someRandomBytes";
    ekrBytes = "ekrBytes";
  }

  @BeforeEach
  void setupTest() throws IOException {
    sessionFactory = SessionFactory
      .newBuilder("product", "service")
      .withInMemoryMetastore()
      .withNeverExpiredCryptoPolicy()
      .withStaticKeyManagementService("mysupersecretstaticmasterkey!!!!")
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

    assertEquals(dataRowRecord.getData(), ByteString.copyFrom(drrBytes.getBytes(StandardCharsets.UTF_8)));
    assertEquals(dataRowRecord.getKey().getKey(), ByteString.copyFrom(ekrBytes.getBytes(StandardCharsets.UTF_8)));
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
      .setData(ByteString.copyFrom(drrBytes.getBytes(StandardCharsets.UTF_8)))
      .setKey(AppEncryptionProtos.EnvelopeKeyRecord.newBuilder()
        .setParentKeyMeta(AppEncryptionProtos.KeyMeta.newBuilder()
          .setCreated(parentKeyMetaCreatedTime)
          .setKeyId(parentKeyMetaKeyId)
          .build())
        .setKey(ByteString.copyFrom(ekrBytes.getBytes(StandardCharsets.UTF_8)))
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
    verify(responseObserver, timeout(100).times(++timesOnNext)).onNext(any(SessionResponse.class));

    // Setup mocking for encrypt response
    AtomicReference<DataRowRecord> dataRowRecord = new AtomicReference<>();
    doAnswer(x -> {
      SessionResponse response = x.getArgument(0);
      dataRowRecord.set(response.getEncryptResponse().getDataRowRecord());
      return null;
    }).when(responseObserver).onNext(any(SessionResponse.class));

    // Try to encrypt a payload
    String originalPayloadString = "mysupersecretpayload";
    ByteString bytes = ByteString.copyFrom(originalPayloadString.getBytes(StandardCharsets.UTF_8));
    Encrypt dataToBeEncrypted = Encrypt.newBuilder().setData(bytes).build();
    requestObserver.onNext(SessionRequest.newBuilder().setEncrypt(dataToBeEncrypted).build());
    verify(responseObserver, timeout(100).times(++timesOnNext)).onNext(any(SessionResponse.class));

    // Setup mock for decrypt response
    AtomicReference<String> decryptedPayload = new AtomicReference<>();
    doAnswer(x -> {
      SessionResponse response = x.getArgument(0);
      ByteString decryptedData = response.getDecryptResponse().getData();
      decryptedPayload.set(new String(decryptedData.toByteArray(), StandardCharsets.UTF_8));
      return null;
    }).when(responseObserver).onNext(any(SessionResponse.class));

    // Try to decrypt back the payload
    Decrypt dataToBeDecrypted = Decrypt.newBuilder().setDataRowRecord(dataRowRecord.get()).build();
    requestObserver.onNext(SessionRequest.newBuilder().setDecrypt(dataToBeDecrypted).build());
    verify(responseObserver, timeout(100).times(++timesOnNext)).onNext(any(SessionResponse.class));

    // Verify that both the payloads match
    assertEquals(originalPayloadString, decryptedPayload.get());

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

    // Verify that we get an error response from the server
    doAnswer(x -> {
      SessionResponse response = x.getArgument(0);
      assertTrue(response.hasErrorResponse());
      return null;
    }).when(responseObserver).onNext(any(SessionResponse.class));

    // Try to encrypt a payload
    String originalPayloadString = "mysupersecretpayload";
    ByteString bytes = ByteString.copyFrom(originalPayloadString.getBytes(StandardCharsets.UTF_8));
    Encrypt dataToBeEncrypted = Encrypt.newBuilder().setData(bytes).build();
    requestObserver.onNext(SessionRequest.newBuilder().setEncrypt(dataToBeEncrypted).build());
    verify(responseObserver, timeout(100).times(++timesOnNext)).onNext(any(SessionResponse.class));

    requestObserver.onCompleted();
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

    // Verify that we get an error response from the server
    doAnswer(x -> {
      SessionResponse response = x.getArgument(0);
      assertTrue(response.hasErrorResponse());
      return null;
    }).when(responseObserver).onNext(any(SessionResponse.class));

    // Try to decrypt something
    Decrypt dataToBeDecrypted = Decrypt.newBuilder().setDataRowRecord(DataRowRecord.getDefaultInstance()).build();
    requestObserver.onNext(SessionRequest.newBuilder().setDecrypt(dataToBeDecrypted).build());
    verify(responseObserver, timeout(100).times(++timesOnNext)).onNext(any(SessionResponse.class));

    requestObserver.onCompleted();
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
    try {
      inProcessChannel.shutdownNow();
    } catch (Exception ignored) {
    }
    verify(responseObserver).onError(any(Throwable.class));
  }
}
