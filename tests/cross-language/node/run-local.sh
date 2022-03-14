#!/bin/bash

set -eux

export MYSQL_HOSTNAME=mysql
export TEST_DB_NAME=testdb
export TEST_DB_USER=root
export TEST_DB_PASSWORD=Password123
export TEST_DB_PORT=3306

function cleanup {
  echo "Kill MySQL container"
  docker kill "${MYSQL_CONTAINER_ID}"
}

echo "Launch MySQL container"
MYSQL_CONTAINER_ID=$(docker run --rm -d --platform linux/amd64 -e MYSQL_HOSTNAME=${MYSQL_HOSTNAME} -e MYSQL_DATABASE=${TEST_DB_NAME} -e MYSQL_USERNAME=${TEST_DB_USER} \
    -e MYSQL_ROOT_PASSWORD=${TEST_DB_PASSWORD} -p 127.0.0.1:${TEST_DB_PORT}:3306/tcp --health-cmd "mysqladmin --protocol=tcp -u root \
    -pPassword123 ping" --health-interval 10s --health-timeout 5s --health-retries 10 mysql:5.7)

# Ensure Docker container is killed
trap cleanup EXIT
trap cleanup INT

echo "Waiting for MySQL to come up"
while ! mysqladmin ping --protocol=tcp -u "${TEST_DB_USER}" -p"${TEST_DB_PASSWORD}" --silent 2>/dev/null; do
    sleep 1
done

echo "Create encryption_key table"
mysql --protocol=tcp -P 3306 -u "${TEST_DB_USER}" -p"${TEST_DB_PASSWORD}" -e "CREATE TABLE ${TEST_DB_NAME}.encryption_key (
          id             VARCHAR(255) NOT NULL,
          created        TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
          key_record     TEXT         NOT NULL,
          PRIMARY KEY (id, created),
          INDEX (created)
        );" 2>/dev/null

npm install

source ./run-encrypt.sh

# Simulate other platforms
echo "Simulate other platforms by copying node_encrypted"
cp /tmp/node_encrypted /tmp/java_encrypted
cp /tmp/node_encrypted /tmp/csharp_encrypted
cp /tmp/node_encrypted /tmp/go_encrypted
cp /tmp/node_encrypted /tmp/sidecar_go_encrypted
cp /tmp/node_encrypted /tmp/sidecar_java_encrypted

source ./run-decrypt.sh

echo "Clean up simulated other platforms"
rm /tmp/node_encrypted /tmp/java_encrypted /tmp/csharp_encrypted /tmp/go_encrypted /tmp/sidecar_go_encrypted /tmp/sidecar_java_encrypted
