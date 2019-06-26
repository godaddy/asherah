FROM openjdk:8-jre-slim

# jna native lib install (helps w/ RO filesystems in particular)
# NOTE: Currently base image is stretch which has 4.2.2 which is not compatible w/ Asherah's JNA version.
# So pulling from testing (currently buster) and then removing the testing repo once we're finished.
RUN echo "deb http://deb.debian.org/debian testing main" >> /etc/apt/sources.list.d/testing.list && \
    apt-get update && \
    apt-get -t testing install -y --no-install-recommends libjna-jni && \
    rm -f /etc/apt/sources.list.d/testing.list && \
    rm -rf /var/lib/apt/lists/*

# artifact setup
ARG JAR_FILE

ENV APP_DIR=/usr/app

RUN mkdir -p ${APP_DIR}
ADD ${JAR_FILE} ${APP_DIR}/app.jar

EXPOSE 8080

# user setup
RUN groupadd -r aeljava && \
    useradd -r -g aeljava -M -s /bin/false -c "Docker image user" aeljava && \
    chown -R aeljava:aeljava ${APP_DIR}

WORKDIR ${APP_DIR}

USER aeljava

# TODO Consider refactoring to a shell script
# Debian package has extra ".system" in library name, so have to override name. If needed, add jna.debug_load.jna=true for help with debugging 
ENTRYPOINT ["java", "-Djna.boot.library.name=jnidispatch.system", "-Djna.nounpack=true", "-jar", "app.jar", "server", "/config.yaml"]