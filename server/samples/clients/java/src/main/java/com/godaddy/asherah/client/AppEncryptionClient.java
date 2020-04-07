package com.godaddy.asherah.client;

import com.godaddy.asherah.grpc.AppEncryptionGrpc;
import com.godaddy.asherah.grpc.AppEncryptionProtos.DataRowRecord;
import com.godaddy.asherah.grpc.AppEncryptionProtos.SessionRequest;
import com.godaddy.asherah.grpc.AppEncryptionProtos.SessionResponse;
import com.godaddy.asherah.grpc.AppEncryptionProtos.GetSession;
import com.godaddy.asherah.grpc.AppEncryptionProtos.Encrypt;
import com.godaddy.asherah.grpc.AppEncryptionProtos.Decrypt;
import com.google.protobuf.ByteString;

import java.nio.charset.StandardCharsets;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

import io.grpc.Channel;
import io.grpc.ManagedChannel;
import io.grpc.netty.NettyChannelBuilder;
import io.grpc.stub.StreamObserver;
import io.netty.channel.EventLoopGroup;
import io.netty.channel.epoll.EpollDomainSocketChannel;
import io.netty.channel.epoll.EpollEventLoopGroup;
import io.netty.channel.kqueue.KQueueDomainSocketChannel;
import io.netty.channel.kqueue.KQueueEventLoopGroup;
import io.netty.channel.unix.DomainSocketAddress;
import org.apache.commons.lang3.SystemUtils;

import static com.godaddy.asherah.grpc.AppEncryptionGrpc.AppEncryptionStub;

public class AppEncryptionClient {

  private final AppEncryptionStub appEncryptionStub;

  private DataRowRecord dataRowRecord;
  private String decryptedPayloadString;

  public AppEncryptionClient(final Channel channel) {
    // It is up to the client to determine whether to block the call or just use async call like the one below
    appEncryptionStub = AppEncryptionGrpc.newStub(channel);
  }

  public CountDownLatch session() {
    final CountDownLatch finishLatch = new CountDownLatch(1);
    StreamObserver<SessionRequest> requestObserver = appEncryptionStub.session(new StreamObserver<SessionResponse>() {
      @Override
      public void onNext(final SessionResponse sessionResponse) {

        // for debug purposes
        // System.out.println("got response from server = " + sessionResponse);

        if (sessionResponse.hasEncryptResponse()) {
          // do something with encrypt response
          dataRowRecord = sessionResponse.getEncryptResponse().getDataRowRecord();
        }

        if (sessionResponse.hasDecryptResponse()) {
          // do something with a decrypt response
          decryptedPayloadString =
              new String(sessionResponse.getDecryptResponse().getData().toByteArray(), StandardCharsets.UTF_8);
        }
      }

      @Override
      public void onError(final Throwable throwable) {
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
    System.out.println("Encrypting payload = " + originalPayloadString);
    ByteString bytes = ByteString.copyFrom(originalPayloadString.getBytes(StandardCharsets.UTF_8));
    Encrypt dataToBeEncrypted = Encrypt.newBuilder().setData(bytes).build();
    requestObserver.onNext(SessionRequest.newBuilder().setEncrypt(dataToBeEncrypted).build());

    try {
      // Wait for response from the server
      Thread.sleep(TimeUnit.SECONDS.toMillis(2));
    }
    catch (InterruptedException e) {
      e.printStackTrace();
    }

    // Try to decrypt back the payload
    Decrypt dataToBeDecrypted = Decrypt.newBuilder().setDataRowRecord(dataRowRecord).build();
    requestObserver.onNext(SessionRequest.newBuilder().setDecrypt(dataToBeDecrypted).build());

    try {
      // Wait for response from the server
      Thread.sleep(TimeUnit.SECONDS.toMillis(2));
    }
    catch (InterruptedException e) {
      e.printStackTrace();
    }

    // Verify that the two payload match
    System.out.println("Decrypted payload = " + decryptedPayloadString);
    System.out.println("matches = " + originalPayloadString.equals(decryptedPayloadString));

    // Mark the end of requests
    requestObserver.onCompleted();

    // return the latch while receiving happens asynchronously
    return finishLatch;
  }

  public static void main(final String[] args) throws InterruptedException {

    final long awaitTime = 5;

    NettyChannelBuilder builder =
        NettyChannelBuilder.forAddress(new DomainSocketAddress("/tmp/appencryption.sock"));
    EventLoopGroup group;
    if (SystemUtils.IS_OS_MAC) {
      group = new KQueueEventLoopGroup();
      builder.channelType(KQueueDomainSocketChannel.class);
    }
    else {
      // For linux client
      group = new EpollEventLoopGroup();
      builder.channelType(EpollDomainSocketChannel.class);
    }
    builder.eventLoopGroup(group);

    // Channel is the abstraction to connect to a service endpoint
    // Let's use plaintext communication because we don't have certs
    ManagedChannel channel = builder.usePlaintext().build();

    try {
      AppEncryptionClient appEncryptionClient = new AppEncryptionClient(channel);

      // Try some operations
      CountDownLatch finishLatch = appEncryptionClient.session();

      if (!finishLatch.await(1, TimeUnit.MINUTES)) {
        System.out.println("Can't finish within 1 minutes");
      }
    }
    finally {
      // A Channel should be shutdown before stopping the process.
      channel.shutdown().awaitTermination(awaitTime, TimeUnit.SECONDS);
    }
  }

}
