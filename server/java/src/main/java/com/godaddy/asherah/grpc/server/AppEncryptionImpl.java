package com.godaddy.asherah.grpc.server;

import io.grpc.stub.StreamObserver;

public class AppEncryptionImpl extends AppEncryptionGrpc.AppEncryptionImplBase {

  @Override
  public StreamObserver<Appencryption.SessionRequest> session(StreamObserver<Appencryption.SessionResponse> responseObserver) {
    return super.session(responseObserver);
  }
}
