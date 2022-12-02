package com.godaddy.asherah.grpc;

import static io.grpc.MethodDescriptor.generateFullMethodName;

/**
 */
@javax.annotation.Generated(
    value = "by gRPC proto compiler (version 1.51.0)",
    comments = "Source: appencryption.proto")
@io.grpc.stub.annotations.GrpcGenerated
public final class AppEncryptionGrpc {

  private AppEncryptionGrpc() {}

  public static final String SERVICE_NAME = "asherah.apps.server.AppEncryption";

  // Static method descriptors that strictly reflect the proto.
  private static volatile io.grpc.MethodDescriptor<com.godaddy.asherah.grpc.AppEncryptionProtos.SessionRequest,
      com.godaddy.asherah.grpc.AppEncryptionProtos.SessionResponse> getSessionMethod;

  @io.grpc.stub.annotations.RpcMethod(
      fullMethodName = SERVICE_NAME + '/' + "Session",
      requestType = com.godaddy.asherah.grpc.AppEncryptionProtos.SessionRequest.class,
      responseType = com.godaddy.asherah.grpc.AppEncryptionProtos.SessionResponse.class,
      methodType = io.grpc.MethodDescriptor.MethodType.BIDI_STREAMING)
  public static io.grpc.MethodDescriptor<com.godaddy.asherah.grpc.AppEncryptionProtos.SessionRequest,
      com.godaddy.asherah.grpc.AppEncryptionProtos.SessionResponse> getSessionMethod() {
    io.grpc.MethodDescriptor<com.godaddy.asherah.grpc.AppEncryptionProtos.SessionRequest, com.godaddy.asherah.grpc.AppEncryptionProtos.SessionResponse> getSessionMethod;
    if ((getSessionMethod = AppEncryptionGrpc.getSessionMethod) == null) {
      synchronized (AppEncryptionGrpc.class) {
        if ((getSessionMethod = AppEncryptionGrpc.getSessionMethod) == null) {
          AppEncryptionGrpc.getSessionMethod = getSessionMethod =
              io.grpc.MethodDescriptor.<com.godaddy.asherah.grpc.AppEncryptionProtos.SessionRequest, com.godaddy.asherah.grpc.AppEncryptionProtos.SessionResponse>newBuilder()
              .setType(io.grpc.MethodDescriptor.MethodType.BIDI_STREAMING)
              .setFullMethodName(generateFullMethodName(SERVICE_NAME, "Session"))
              .setSampledToLocalTracing(true)
              .setRequestMarshaller(io.grpc.protobuf.ProtoUtils.marshaller(
                  com.godaddy.asherah.grpc.AppEncryptionProtos.SessionRequest.getDefaultInstance()))
              .setResponseMarshaller(io.grpc.protobuf.ProtoUtils.marshaller(
                  com.godaddy.asherah.grpc.AppEncryptionProtos.SessionResponse.getDefaultInstance()))
              .setSchemaDescriptor(new AppEncryptionMethodDescriptorSupplier("Session"))
              .build();
        }
      }
    }
    return getSessionMethod;
  }

  /**
   * Creates a new async stub that supports all call types for the service
   */
  public static AppEncryptionStub newStub(io.grpc.Channel channel) {
    io.grpc.stub.AbstractStub.StubFactory<AppEncryptionStub> factory =
      new io.grpc.stub.AbstractStub.StubFactory<AppEncryptionStub>() {
        @java.lang.Override
        public AppEncryptionStub newStub(io.grpc.Channel channel, io.grpc.CallOptions callOptions) {
          return new AppEncryptionStub(channel, callOptions);
        }
      };
    return AppEncryptionStub.newStub(factory, channel);
  }

  /**
   * Creates a new blocking-style stub that supports unary and streaming output calls on the service
   */
  public static AppEncryptionBlockingStub newBlockingStub(
      io.grpc.Channel channel) {
    io.grpc.stub.AbstractStub.StubFactory<AppEncryptionBlockingStub> factory =
      new io.grpc.stub.AbstractStub.StubFactory<AppEncryptionBlockingStub>() {
        @java.lang.Override
        public AppEncryptionBlockingStub newStub(io.grpc.Channel channel, io.grpc.CallOptions callOptions) {
          return new AppEncryptionBlockingStub(channel, callOptions);
        }
      };
    return AppEncryptionBlockingStub.newStub(factory, channel);
  }

  /**
   * Creates a new ListenableFuture-style stub that supports unary calls on the service
   */
  public static AppEncryptionFutureStub newFutureStub(
      io.grpc.Channel channel) {
    io.grpc.stub.AbstractStub.StubFactory<AppEncryptionFutureStub> factory =
      new io.grpc.stub.AbstractStub.StubFactory<AppEncryptionFutureStub>() {
        @java.lang.Override
        public AppEncryptionFutureStub newStub(io.grpc.Channel channel, io.grpc.CallOptions callOptions) {
          return new AppEncryptionFutureStub(channel, callOptions);
        }
      };
    return AppEncryptionFutureStub.newStub(factory, channel);
  }

  /**
   */
  public static abstract class AppEncryptionImplBase implements io.grpc.BindableService {

    /**
     * <pre>
     * Performs session operations for a single partition.
     * Each session must begin with a GetSession message with all subsequent
     * Encrypt and Decrypt operations scoped its partition.
     * </pre>
     */
    public io.grpc.stub.StreamObserver<com.godaddy.asherah.grpc.AppEncryptionProtos.SessionRequest> session(
        io.grpc.stub.StreamObserver<com.godaddy.asherah.grpc.AppEncryptionProtos.SessionResponse> responseObserver) {
      return io.grpc.stub.ServerCalls.asyncUnimplementedStreamingCall(getSessionMethod(), responseObserver);
    }

    @java.lang.Override public final io.grpc.ServerServiceDefinition bindService() {
      return io.grpc.ServerServiceDefinition.builder(getServiceDescriptor())
          .addMethod(
            getSessionMethod(),
            io.grpc.stub.ServerCalls.asyncBidiStreamingCall(
              new MethodHandlers<
                com.godaddy.asherah.grpc.AppEncryptionProtos.SessionRequest,
                com.godaddy.asherah.grpc.AppEncryptionProtos.SessionResponse>(
                  this, METHODID_SESSION)))
          .build();
    }
  }

  /**
   */
  public static final class AppEncryptionStub extends io.grpc.stub.AbstractAsyncStub<AppEncryptionStub> {
    private AppEncryptionStub(
        io.grpc.Channel channel, io.grpc.CallOptions callOptions) {
      super(channel, callOptions);
    }

    @java.lang.Override
    protected AppEncryptionStub build(
        io.grpc.Channel channel, io.grpc.CallOptions callOptions) {
      return new AppEncryptionStub(channel, callOptions);
    }

    /**
     * <pre>
     * Performs session operations for a single partition.
     * Each session must begin with a GetSession message with all subsequent
     * Encrypt and Decrypt operations scoped its partition.
     * </pre>
     */
    public io.grpc.stub.StreamObserver<com.godaddy.asherah.grpc.AppEncryptionProtos.SessionRequest> session(
        io.grpc.stub.StreamObserver<com.godaddy.asherah.grpc.AppEncryptionProtos.SessionResponse> responseObserver) {
      return io.grpc.stub.ClientCalls.asyncBidiStreamingCall(
          getChannel().newCall(getSessionMethod(), getCallOptions()), responseObserver);
    }
  }

  /**
   */
  public static final class AppEncryptionBlockingStub extends io.grpc.stub.AbstractBlockingStub<AppEncryptionBlockingStub> {
    private AppEncryptionBlockingStub(
        io.grpc.Channel channel, io.grpc.CallOptions callOptions) {
      super(channel, callOptions);
    }

    @java.lang.Override
    protected AppEncryptionBlockingStub build(
        io.grpc.Channel channel, io.grpc.CallOptions callOptions) {
      return new AppEncryptionBlockingStub(channel, callOptions);
    }
  }

  /**
   */
  public static final class AppEncryptionFutureStub extends io.grpc.stub.AbstractFutureStub<AppEncryptionFutureStub> {
    private AppEncryptionFutureStub(
        io.grpc.Channel channel, io.grpc.CallOptions callOptions) {
      super(channel, callOptions);
    }

    @java.lang.Override
    protected AppEncryptionFutureStub build(
        io.grpc.Channel channel, io.grpc.CallOptions callOptions) {
      return new AppEncryptionFutureStub(channel, callOptions);
    }
  }

  private static final int METHODID_SESSION = 0;

  private static final class MethodHandlers<Req, Resp> implements
      io.grpc.stub.ServerCalls.UnaryMethod<Req, Resp>,
      io.grpc.stub.ServerCalls.ServerStreamingMethod<Req, Resp>,
      io.grpc.stub.ServerCalls.ClientStreamingMethod<Req, Resp>,
      io.grpc.stub.ServerCalls.BidiStreamingMethod<Req, Resp> {
    private final AppEncryptionImplBase serviceImpl;
    private final int methodId;

    MethodHandlers(AppEncryptionImplBase serviceImpl, int methodId) {
      this.serviceImpl = serviceImpl;
      this.methodId = methodId;
    }

    @java.lang.Override
    @java.lang.SuppressWarnings("unchecked")
    public void invoke(Req request, io.grpc.stub.StreamObserver<Resp> responseObserver) {
      switch (methodId) {
        default:
          throw new AssertionError();
      }
    }

    @java.lang.Override
    @java.lang.SuppressWarnings("unchecked")
    public io.grpc.stub.StreamObserver<Req> invoke(
        io.grpc.stub.StreamObserver<Resp> responseObserver) {
      switch (methodId) {
        case METHODID_SESSION:
          return (io.grpc.stub.StreamObserver<Req>) serviceImpl.session(
              (io.grpc.stub.StreamObserver<com.godaddy.asherah.grpc.AppEncryptionProtos.SessionResponse>) responseObserver);
        default:
          throw new AssertionError();
      }
    }
  }

  private static abstract class AppEncryptionBaseDescriptorSupplier
      implements io.grpc.protobuf.ProtoFileDescriptorSupplier, io.grpc.protobuf.ProtoServiceDescriptorSupplier {
    AppEncryptionBaseDescriptorSupplier() {}

    @java.lang.Override
    public com.google.protobuf.Descriptors.FileDescriptor getFileDescriptor() {
      return com.godaddy.asherah.grpc.AppEncryptionProtos.getDescriptor();
    }

    @java.lang.Override
    public com.google.protobuf.Descriptors.ServiceDescriptor getServiceDescriptor() {
      return getFileDescriptor().findServiceByName("AppEncryption");
    }
  }

  private static final class AppEncryptionFileDescriptorSupplier
      extends AppEncryptionBaseDescriptorSupplier {
    AppEncryptionFileDescriptorSupplier() {}
  }

  private static final class AppEncryptionMethodDescriptorSupplier
      extends AppEncryptionBaseDescriptorSupplier
      implements io.grpc.protobuf.ProtoMethodDescriptorSupplier {
    private final String methodName;

    AppEncryptionMethodDescriptorSupplier(String methodName) {
      this.methodName = methodName;
    }

    @java.lang.Override
    public com.google.protobuf.Descriptors.MethodDescriptor getMethodDescriptor() {
      return getServiceDescriptor().findMethodByName(methodName);
    }
  }

  private static volatile io.grpc.ServiceDescriptor serviceDescriptor;

  public static io.grpc.ServiceDescriptor getServiceDescriptor() {
    io.grpc.ServiceDescriptor result = serviceDescriptor;
    if (result == null) {
      synchronized (AppEncryptionGrpc.class) {
        result = serviceDescriptor;
        if (result == null) {
          serviceDescriptor = result = io.grpc.ServiceDescriptor.newBuilder(SERVICE_NAME)
              .setSchemaDescriptor(new AppEncryptionFileDescriptorSupplier())
              .addMethod(getSessionMethod())
              .build();
        }
      }
    }
    return result;
  }
}
