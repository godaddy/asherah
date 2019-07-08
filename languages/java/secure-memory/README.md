# SecureMemory Java Edition
This class provides a way for applications to keep secret information (like cryptographic keys) in an area of memory
that is secure in the described ways.

## Currently supported / tested platforms
* MacOS x86-64
* Linux x86-64

## Goals

* Provide an interface which allows safe patterns of secrets usage
* Provide a handle for passing secrets around that doesn't expose the secret until it's actually used
* Minimize the lifetime of secrets in managed memory

## Guarantees
Any implementation must have the following guarantees in so far as secret information stored in secure memory

* Values stored will not show up in core dumps
* Values stored will not be swapped
* Values stored will be securely / explicitly zeroed out when no longer in use

## Protected Memory Implementation
The protected memory implementation of secure memory

* Uses mlock to lock the pages to prevent swapping
* Uses mprotect to mark the pages no-access until needed
* If the operating system supports it, uses madvise to disallow core dumps of protected regions
* If the operating system does not support madvise, uses setrlimit to disable core dumps entirely
* If the operating system supports it, uses madvise to request that mapped pages be zeroed on exit

## Todo

* Add support for Cleaner rather than finalizer
* Add unit tests that generate core dumps and scan them for secrets (need to extract gcore source)

## Usage

```java
SecretFactory secretFactory = new ProtectedMemorySecretFactory();

try (Secret secretKey = secretFactory.createSecret(getSecretFromStore())) {
  secret.withSecretBytes(bytes -> {
    doSomethingWithSecretBytes(bytes);
  });
}
```

## Library Development Notes

### Running Tests Locally via Docker Image
Below is an example of how to run tests locally using a Docker image. This one is using the build image used for
Jenkins build/deployment, but could be replaced with other images for different targeted platforms. Note this is run
from your project directory.

```console
[user@machine SecureMemoryJava]$ docker build images/build/
...
Successfully built <generated_image_id>
[user@machine SecureMemoryJava]$ docker run -it --rm -v $HOME/.m2:/home/jenkins/.m2 -v "$PWD":/usr/app/src -w /usr/app/src --ulimit memlock=-1:-1 --ulimit core=-1:-1 <generated_image_id> mvn clean install
...
```
*Note*: The above build is known to work on macOS due to how the bind mounts map UIDs. On Linux systems you will likely
need to add the optional build arguments:

``` console
[user@machine SecureMemoryJava]$ docker build --build-arg UID=$(id -u) --build-arg GID=$(id -g) images/build
```

This will create the container's user with your UID so that it has full access to the .m2 directory.
