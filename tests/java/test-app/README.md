# test-app

Service runs a REST API that exposes suites of JUnit tests that can be run.

## REST API Spec

NOTE: This isn't a REST API in the traditional sense. We're simply using GET requests to kick off tests with request parameters
and they happen to return a JSON response. The meaningful outputs are the success/failure counts in the response and individual
events (results) which can be output to different result exporters.

TODO Replace w/ swagger/openapi

### App Encryption Tests

#### Regression Suite

```javascript
GET /v1/java/appencryption/regression

Description: Runs a suite of regression tests intended to serve as sanity/functional tests. Tests include sanity checks around
roundtrip encryption/decryption of data and a whitebox functional test that enumerates all of the possible states the system and
intermediate keys can be in the metastore and internal caches.

Query Parameters:
  N/A

Response:
200
{
  "endpoint": "v1/java/appencryption/regression",
  "failed_count": 0,
  "failures": [], // would include a summary of failed test messages if failed_count > 0
  "success_count": 95,
  "timestamp": "2019-04-09T18:22:03.343Z",
  "total_count": 95
}
```

#### Multi-threaded Suite (Sync)

```javascript
GET /v1/java/appencryption/multithreaded

Description: Runs a suite of multi-threaded tests intended to stress test various concurrency semantics of primary client-side
interfaces used in the AppEncryption library. Tests include encrypting the same payload concurrently and verifying being able to
decrypt all resulting DRRs, using multiple SessionFactory instances concurrently with the same and multiple
partitions (uncommon use cases), and using the same SessionFactory instance concurrently with multiple partitions
(common use case).

Query Parameters:
  numIterations: optional int, default 100, the number of iterations for any requests that execute a loop
  numRequests: optional int, default 100, the number of requests to submit to a pool of workers
  numThreads: optional int, default 100, the number of threads to use for a pool of workers
  payloadSizeBytes: optional int, default 100, the size of the payload to generate for tests (note JSON-based tests will be slightly larger)
  threadPoolTimeoutSeconds: optional long, default 60, the number of seconds to wait for the pool of workers to finish before timing out

Response:
200
{
  "endpoint": "v1/java/appencryption/multithreaded",
  "failed_count": 0,
  "failures": [], // would include a summary of failed test messages if failed_count > 0
  "success_count": 6,
  "timestamp": "2019-04-09T18:21:09.337Z",
  "total_count": 6
}
```

#### Multi-threaded Suite (Async)

```javascript
GET /v1/java/appencryption/async/multithreaded

Description: Runs the same suite of multi-threaded tests as the above but runs them in the background and returns an immediate response.

Query Parameters:
  numIterations: optional int, default 100, the number of iterations for any requests that execute a loop
  numRequests: optional int, default 100, the number of requests to submit to a pool of workers
  numThreads: optional int, default 100, the number of threads to use for a pool of workers
  payloadSizeBytes: optional int, default 100, the size of the payload to generate for tests (note JSON-based tests will be slightly larger)
  threadPoolTimeoutSeconds: optional long, default 60, the number of seconds to wait for the pool of workers to finish before timing out

Response:
202
{
  "endpoint": "v1/java/appencryption/async/multithreaded",
  "timestamp": "2019-04-09T18:55:04.098Z"
}
```

### Secure Memory Tests

#### Multi-threaded Suite (Sync)

```javascript
GET /v1/java/securememory/multithreaded

Description: Runs a suite of multi-threaded tests intended to stress test various concurrency semantics of primary client-side
interfaces used in the SecureMemory library. Tests include reading the same Secret instance concurrently.

Query Parameters:
  numRequests: optional int, default 100, the number of requests to submit to a pool of workers
  numThreads: optional int, default 100, the number of threads to use for a pool of workers
  payloadSizeBytes: optional int, default 100, the size of the payload to generate for tests (note JSON-based tests will be slightly larger)
  threadPoolTimeoutSeconds: optional long, default 60, the number of seconds to wait for the pool of workers to finish before timing out

Response:
200
{
  "endpoint": "v1/java/securememory/multithreaded",
  "failed_count": 0,
  "failures": [], // would include a summary of failed test messages if failed_count > 0
  "success_count": 1,
  "timestamp": "2019-04-09T18:23:28.619Z",
  "total_count": 1
}
```

#### Multi-threaded Suite (Async)

```javascript
GET /v1/java/securememory/async/multithreaded

Description: Runs the same suite of multi-threaded tests as the above, but runs them in the background and returns an immediate response.

Query Parameters:
  numRequests: optional int, default 100, the number of requests to submit to a pool of workers
  numThreads: optional int, default 100, the number of threads to use for a pool of workers
  payloadSizeBytes: optional int, default 100, the size of the payload to generate for tests (note JSON-based tests will be slightly larger)
  threadPoolTimeoutSeconds: optional long, default 60, the number of seconds to wait for the pool of workers to finish before timing out

Response:
202
{
  "endpoint": "v1/java/securememory/async/multithreaded",
  "timestamp": "2019-04-09T18:54:49.010Z"
}
```


## How to Build and Run

To build, simply run `mvn clean install`. The maven build will generate multiple Docker images for commonly-used image bases.

To launch a container of one of the generated images, run:

```console
# Using default configs, read-only FS, and exposing container port on random host port
[user@machine test-app]$ docker run -it -P --read-only <image_id>
...

# Using AWS configs and exporting result to KINESIS
[user@machine test-app]$ export AWS_REGION=<preferred-region>
[user@machine test-app]$ docker run -it -P -e EXPORT_TO_KINESIS=true -e AWS_SECRET_ACCESS_KEY=$AWS_SECRET_ACCESS_KEY -e AWS_ACCESS_KEY_ID=$AWS_ACCESS_KEY_ID -e AWS_SESSION_TOKEN=$AWS_SESSION_TOKEN -e AWS_REGION=$AWS_REGION --read-only <image_id>
...

# Using default configs but overriding with env variables, read-only FS, and exposing container port on random host port
[user@machine test-app]$ docker run -it -P --read-only -e SOME_CONFIG=override_value <image_id>
...
```

TODO Add config section that explains how to configure integrating with external resources, etc.

## Prerequisites for Docker read-only containers
JNA attempts to unpack the native libraries from the jar but this would fail in a read only container. To ensure that this works we need some work-around for specific containers.
We need to add the following properties in java exec
1. `-Djna.nounpack=true` : Never attempt to unpack bundled native libs from the JNA jars
2. `-Djna.boot.library.name=jnidispatch.system` : Specific to Ubuntu and Debian based images due to some conflicts in the naming in public `apt` repos
3. `-Djna.boot.library.path=/usr/lib/x86_64-linux-gnu/jni/` : Specific to `adoptopenjdk/openjdk` base image as it does not have the directories required by JNA in the default library paths.

Ubuntu and Debian package have an extra ".system" in the library name, so that has to be overridden.

Debian package requires their `testing` repo in order to get JNA that is compatible with Asherah. Just this package is installed after which the repo is removed from the apt sources.
