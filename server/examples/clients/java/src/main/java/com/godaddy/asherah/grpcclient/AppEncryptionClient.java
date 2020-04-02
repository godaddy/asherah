package com.godaddy.asherah.grpcclient;

import com.godaddy.asherah.grpc.AppEncryptionGrpc;
import com.google.protobuf.ByteString;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

import io.grpc.Channel;
import io.grpc.ManagedChannel;
import io.grpc.ManagedChannelBuilder;
import io.grpc.stub.StreamObserver;

import static com.godaddy.asherah.grpc.AppEncryptionGrpc.AppEncryptionStub;
import static com.godaddy.asherah.grpc.AppEncryptionProtos.*;

public class AppEncryptionClient {

  private final AppEncryptionStub appEncryptionStub;
  private final List<DataRowRecord> dataRowRecordList;

  public AppEncryptionClient(Channel channel) {
    // It is up to the client to determine whether to block the call or just use async call like the one below
    appEncryptionStub = AppEncryptionGrpc.newStub(channel);
    dataRowRecordList = new ArrayList<>();
  }

  public CountDownLatch session() {
    final CountDownLatch finishLatch = new CountDownLatch(1);
    StreamObserver<SessionRequest> requestObserver = appEncryptionStub.session(new StreamObserver<SessionResponse>() {
      @Override
      public void onNext(SessionResponse sessionResponse) {
        System.out.println("onNext in client");
        System.out.println("got response from server = " + sessionResponse);

        if (sessionResponse.hasEncryptResponse()) {
          dataRowRecordList.add(sessionResponse.getEncryptResponse().getDataRowRecord());
        }

        if (sessionResponse.hasDecryptResponse()) {
          // do something with a decrypt response
        }
      }

      @Override
      public void onError(Throwable throwable) {
        System.out.println("Session failed");
        throwable.printStackTrace();
        finishLatch.countDown();
      }

      @Override
      public void onCompleted() {
        System.out.println("Finished session");
        finishLatch.countDown();
      }
    });

    // Get a session from the server
    GetSession getSession = GetSession.newBuilder().setPartitionId("partition-1").build();
    requestObserver.onNext(SessionRequest.newBuilder().setGetSession(getSession).build());

    // Try to encrypt a payload
    String originalPayloadString = "mysupersecretpayload";
    ByteString bytes = ByteString.copyFrom(originalPayloadString.getBytes(StandardCharsets.UTF_8));
    Encrypt dataToBeEncrypted = Encrypt.newBuilder().setData(bytes).build();
    requestObserver.onNext(SessionRequest.newBuilder().setEncrypt(dataToBeEncrypted).build());

    try {
      // Wait for response from the server
      Thread.sleep(2000);
    } catch (InterruptedException e) {
      e.printStackTrace();
    }

//    System.out.println("SIZE = " + dataRowRecordList.size());
//    DataRowRecord dataRowRecord = dataRowRecordList.get(0);
//    Decrypt dataToBeDecrypted = Decrypt.newBuilder().setDataRowRecord(dataRowRecord).build();
//    requestObserver.onNext(SessionRequest.newBuilder().setDecrypt(dataToBeDecrypted).build());

    // Mark the end of requests
    requestObserver.onCompleted();

    // return the latch while receiving happens asynchronously
    return finishLatch;
  }

  public static void main(final String[] args) throws IOException, InterruptedException {
    // Channel is the abstraction to connect to a service endpoint
    // Let's use plaintext communication because we don't have certs
    ManagedChannel channel = ManagedChannelBuilder.forAddress("localhost", 50000).usePlaintext().build();

    try {
      AppEncryptionClient appEncryptionClient = new AppEncryptionClient(channel);

      // Try some operations
      CountDownLatch finishLatch = appEncryptionClient.session();

      if (!finishLatch.await(1, TimeUnit.MINUTES)) {
        System.out.println("Can't finish within 1 minutes");
      }
    } finally {
      // A Channel should be shutdown before stopping the process.
      channel.shutdown().awaitTermination(5, TimeUnit.SECONDS);
    }
  }

}
