ARG PLATFORM=linux/amd64
FROM --platform=${PLATFORM} maven:3.8-openjdk-17 as mvn-build
RUN mkdir -p /usr/src/server/java
WORKDIR /usr/src/server/java
COPY ./java/*.xml /usr/src/server/java/
RUN mvn dependency:copy-dependencies

COPY ./protos /usr/src/server/protos
COPY ./java/src /usr/src/server/java/src
RUN mvn clean install -DskipTests


FROM --platform=${PLATFORM} eclipse-temurin:17.0.5_8-jre-alpine

# jna native lib install to support RO filesystems
RUN apk add --no-cache java-jna-native

ENV APP_DIR=/usr/app
RUN mkdir -p ${APP_DIR}

# user setup
RUN addgroup -S aeljava && \
    adduser -S -G aeljava -H -D -s /bin/false -g "Docker image user" aeljava && \
    chown -R aeljava:aeljava ${APP_DIR}

COPY --from=mvn-build /usr/src/server/java/target/grpc-server-1.0.0-SNAPSHOT-jar-with-dependencies.jar ${APP_DIR}/app.jar

# default values for all required options
ENV ASHERAH_KMS_MODE=static
ENV ASHERAH_METASTORE_MODE=memory
ENV ASHERAH_PRODUCT_NAME=product
ENV ASHERAH_SERVICE_NAME=service
ENV ASHERAH_EXPIRE_AFTER=90
ENV ASHERAH_CHECK_INTERVAL=60

WORKDIR ${APP_DIR}

USER aeljava

# TODO Consider refactoring to a shell script
ENTRYPOINT ["java", "-Djna.boot.library.path=/usr/lib", "-Djna.nounpack=true", "-jar", "app.jar"]
