# use php alpine
FROM library/php:alpine AS php
ARG ASHERAH_UID=1982
ARG ASHERAH_GID=1982
# add Composer and its dependencies
COPY --from=library/composer /usr/bin/composer /usr/bin/composer
RUN apk add git
# enable gRPC for PHP
RUN apk add \
        autoconf \
        build-base \
        linux-headers \
        zlib-dev \
 && LIBRARY_PATH=/lib:/usr/lib pecl install grpc \
 && docker-php-ext-enable grpc \
 && rm -rf /tmp/pear ~/.pearrc
# set up a non-root user
RUN addgroup -S -g ${ASHERAH_GID} asherah \
 && adduser -S -G asherah -H -D -s /bin/false -g "Docker image user" -u ${ASHERAH_UID} asherah \
 && mkdir /app \
 && chown -R asherah:asherah /app \
 && mkdir /sock \
 && chown -R asherah:asherah /sock
# copy client code
COPY . /app
# start in the application directory
USER asherah
WORKDIR /app
# build dependencies
RUN composer update --no-dev --no-interaction --quiet -o
# set entrypoint for application
ENTRYPOINT [ "php", "/app/appencryption_client.php" ]