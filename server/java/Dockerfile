FROM openjdk:8-jre-alpine

# jna native lib install to support RO filesystems
RUN apk update && \
    apk upgrade && \
    apk add java-jna-native

# TODO Add a multi-stage build once we have a public artifact released.

# artifact setup
ARG JAR_FILE

ENV APP_DIR=/usr/app
RUN mkdir -p ${APP_DIR}

# user setup
RUN addgroup -S aeljava && \
    adduser -S -G aeljava -H -D -s /bin/false -g "Docker image user" aeljava && \
    chown -R aeljava:aeljava ${APP_DIR}

ADD ${JAR_FILE} ${APP_DIR}/app.jar

WORKDIR ${APP_DIR}

USER aeljava

# TODO Consider refactoring to a shell script
ENTRYPOINT ["java", "-Djna.nounpack=true", "-jar", "app.jar"]
