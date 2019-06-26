FROM adoptopenjdk/openjdk8

# jna native lib install (helps w/ RO filesystems in particular)
RUN apt-get update && \
    apt-get install -y libjna-jni && \
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
# Base image doesn't setup as many library paths for java, so have to point JNA boot path to proper dir.
# Ubuntu package has extra ".system" in library name, so have to override name. If needed, add jna.debug_load.jna=true for help with debugging 
ENTRYPOINT ["java", "-Djna.boot.library.path=/usr/lib/x86_64-linux-gnu/jni/", "-Djna.boot.library.name=jnidispatch.system", "-Djna.nounpack=true", "-jar", "app.jar", "server", "/config.yaml"]