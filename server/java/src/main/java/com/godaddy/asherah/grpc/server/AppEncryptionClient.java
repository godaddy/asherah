package com.godaddy.asherah.grpc.server;

import com.google.protobuf.ByteString;
import io.grpc.ManagedChannel;
import io.grpc.ManagedChannelBuilder;
import io.grpc.stub.StreamObserver;
import picocli.CommandLine;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.Map;
import java.util.concurrent.Callable;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.atomic.AtomicReference;

public class AppEncryptionClient {

  public static void main(final String[] args) throws IOException, InterruptedException {
    // Channel is the abstraction to connect to a service endpoint
    // Let's use plaintext communication because we don't have certs
    ManagedChannel channel = ManagedChannelBuilder.forAddress("localhost", 50000).usePlaintext().build();

    // It is up to the client to determine whether to block the call or just use async call like the one below
    AppEncryptionGrpc.AppEncryptionStub serviceStub = AppEncryptionGrpc.newStub(channel);

//    AtomicReference<StreamObserver<Appencryption.SessionRequest>> sessionRequestObserverRef = new AtomicReference<>();
    CountDownLatch finishedLatch = new CountDownLatch(1);

    // Finally, make the call using the stub
    StreamObserver<Appencryption.SessionRequest> observerRef = serviceStub.session(new StreamObserver<Appencryption.SessionResponse>() {
      @Override
      public void onNext(Appencryption.SessionResponse sessionResponse) {
        System.out.println("onNext from client");
        System.out.println("got response from server");

        System.out.println("response = " + sessionResponse);

//        sessionRequestObserverRef.get().onNext(Appencryption.SessionRequest.getDefaultInstance());
      }

      @Override
      public void onError(Throwable throwable) {
        System.out.println("on error from client");
        throwable.printStackTrace();
      }

      @Override
      public void onCompleted() {
        System.out.println("onCompleted from client");
        finishedLatch.countDown();
      }
    });

//    sessionRequestObserverRef.set(observerRef);
    // Get a session from the server
    Appencryption.GetSession getSession = Appencryption.GetSession.newBuilder().setPartitionId("partition-1").build();
    observerRef.onNext(Appencryption.SessionRequest.newBuilder().setGetSession(getSession).build());

    // Try to encrypt a payload
    String originalPayloadString = "mysupersecretpayload";
    ByteString bytes = ByteString.copyFrom(originalPayloadString.getBytes(StandardCharsets.UTF_8));
    Appencryption.Encrypt dataToBeEncrypted = Appencryption.Encrypt.newBuilder().setData(bytes).build();
    observerRef.onNext(Appencryption.SessionRequest.newBuilder().setEncrypt(dataToBeEncrypted).build());

    // Try to decrypt it back


    finishedLatch.await();
    observerRef.onCompleted();

    // A Channel should be shutdown before stopping the process.
    channel.shutdown();
  }

}
