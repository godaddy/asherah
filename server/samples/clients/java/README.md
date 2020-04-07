# gRPC Client (JAVA) for Asherah Server
A simple client application that demonstrates the use use of grpc-java to integrate with Asherah Server.

## Running the client
Ensure the Asherah Server is running locally and listening on `unix:///tmp/appencryption.sock`. Then run:

```java
[user@machine java]$ mvn clean install
[user@machine java]$ java -jar <jar-path>
```

