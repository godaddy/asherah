[![License](https://img.shields.io/github/license/godaddy/asherah.svg)](https://github.com/godaddy/asherah/blob/main/LICENSE)
[![CircleCI](https://img.shields.io/circleci/build/gh/godaddy/asherah.svg)](https://circleci.com/gh/godaddy/asherah)
[![Codecov](https://codecov.io/gh/godaddy/asherah/graph/badge.svg)](https://codecov.io/gh/godaddy/asherah)

# Asherah

An application-layer encryption SDK that provides advanced encryption features and defense in depth against compromise.

Its goal is to provide an easy-to-use library which abstracts away internal complexity and provides rapid, frequent key
rotation with enterprise scale in mind.

## Table of Contents

   * [Introduction](#introduction)
   * [Getting Started](#getting-started)
   * [Sample Applications](#sample-applications)
   * [Further Reading](#further-reading)
   * [Supported Languages](#supported-languages)
       * [Feature Support](#feature-support)
   * [Contributing](#contributing)

## Introduction

Asherah makes use of multiple layers of keys in conjunction with a technique known as "envelope encryption". Envelope encryption is a
practice where a key used to encrypt data is itself encrypted by a higher-order key and stored alongside the encrypted data, hence forming an
envelope structure. The master key used at the root of the key hierarchy is typically managed by a Hardware Security Module (HSM)
or Key Management Service (KMS).

The SDK generates cryptographically strong intermediate keys in the hierarchical model and manages their storage via a pluggable
backing datastore. The integration with a HSM or KMS provider for the root (master) key in the hierarchy is implemented using a
similar pluggable model. This allows for supporting a wide variety of datastores and cloud providers for different architectures.

The SDK provides implementations in multiple languages using native interoperability mechanisms to securely manage and
cache internally-generated keys in off-heap protected memory. The combination of secure memory management and the hierarchical
key model's partitioning help minimize attack exposure in the event of compromise. Using the protected memory cache has an added
benefit of reducing interactions with external resources to improve latency and minimize incurred costs.

## Getting Started

The basic use of the SDK proceeds in 3 steps:

### Step 1: Create a session factory

A session factory is required to generate encryption/decryption sessions. For simplicity, the session factory uses the
builder pattern, specifically a _step builder_. This ensures all required properties are set before a factory is built.

To obtain an instance of the builder, use the static factory method `newBuilder`. Once you have a builder, you can
use the `withXXX` setter methods to configure the session factory properties.

Below is an example of using `memguard-rs`, the Rust port:

```rust,no_run
use memguard::{Buffer, Enclave, Stream, catch_interrupt, purge, MemguardError};
use std::io::{Read, Write};

// Invert the bytes in a buffer, using an Enclave for protection.
fn invert_buffer_securely(input_buffer: &mut Buffer) -> Result<Enclave, MemguardError> {
    // Make the buffer mutable if it wasn't (e.g., if it came from Enclave::open)
    input_buffer.melt()?;

    input_buffer.with_data_mut(|data| {
        for byte in data.iter_mut() {
            *byte = !*byte;
        }
        Ok(())
    })??; // Flatten nested Result

    // Re-seal the modified buffer. This also destroys input_buffer.
    Enclave::seal(input_buffer)
}

fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Setup signal handling for graceful shutdown
    catch_interrupt().expect("Failed to set up interrupt handler");

    // Create some initial sensitive data
    let mut original_data = vec![0x0F, 0xF0, 0xAA, 0x55];
    let mut initial_buffer = Buffer::new_from_bytes(&mut original_data)?;

    println!("Original buffer created with size: {}", initial_buffer.size());

    // Process it securely
    let processed_enclave = match invert_buffer_securely(&mut initial_buffer) {
        Ok(enclave) => enclave,
        Err(e) => {
            eprintln!("Error during secure inversion: {:?}", e);
            memguard::purge(); // Ensure cleanup on error path
            return Err(Box::new(e));
        }
    };

    assert!(!initial_buffer.is_alive()); // Consumed by Enclave::seal

    // Open the processed enclave to verify
    let final_buffer = processed_enclave.open()?;
    final_buffer.with_data(|data| {
        println!("Processed data: {:?}", data);
        assert_eq!(data, &[!0x0F, !0xF0, !0xAA, !0x55]);
        Ok(())
    })??;
    final_buffer.destroy()?;

    println!("Successfully processed data.");
    Ok(())
}
```

### Step 2: Create a session

Use the factory to create a session.

```java
Session<byte[], byte[]> sessionBytes = sessionFactory.getSessionBytes("shopper123");
```

The scope of a session is limited to a partition id, i.e. every partition id should have its own session. Also note
that a payload encrypted using some partition id, cannot be decrypted using a different one.

### Step 3: Use the session to accomplish the cryptographic task

The SDK supports 2 usage patterns:

#### Encrypt / Decrypt

This usage style is similar to common encryption utilities where payloads are simply encrypted and
decrypted, and it is completely up to the calling application for storage responsibility.

```java
String originalPayloadString = "mysupersecretpayload";

// encrypt the payload
byte[] dataRowRecordBytes = sessionBytes.encrypt(originalPayloadString.getBytes(StandardCharsets.UTF_8));

// decrypt the payload
String decryptedPayloadString = new String(sessionBytes.decrypt(dataRowRecordBytes), StandardCharsets.UTF_8);
```

#### Store / Load

This pattern uses a key-value/document storage model. A `Session` can accept a `Persistence`
implementation and hooks into its load and store calls.

Example `HashMap`-backed `Persistence` implementation:

```java
Persistence dataPersistence = new Persistence<JSONObject>() {

  Map<String, JSONObject> mapPersistence = new HashMap<>();

  @Override
  public Optional<JSONObject> load(String key) {
    return Optional.ofNullable(mapPersistence.get(key));
  }

  @Override
  public void store(String key, JSONObject value) {
    mapPersistence.put(key, value);
  }
};
```

Putting it all together, an example end-to-end use of the store and load calls:

```java
// Encrypts the payload, stores it in the dataPersistence and returns a look up key
String persistenceKey = sessionJson.store(originalPayload.toJsonObject(), dataPersistence);

// Uses the persistenceKey to look-up the payload in the dataPersistence, decrypts the payload if any and then returns it
Optional<JSONObject> payload = sessionJson.load(persistenceKey, dataPersistence);
```

## Sample Applications

The [samples](/samples) directory includes sample applications that demonstrate use of Asherah SDK using various
languages and platforms.

## Further Reading

* [Design And Architecture](docs/DesignAndArchitecture.md)
* [System Requirements](docs/SystemRequirements.md)
* [Key Management Service](docs/KeyManagementService.md)
* [Metastore](docs/Metastore.md)
* [Key Caching](docs/KeyCaching.md)
* [Code Structure](docs/CodeStructure.md)
* [SDK Internals](docs/Internals.md)
* [Testing Approach](docs/TestingApproach.md)
* [FAQ](docs/FAQ.md)

## Supported Languages

* [Java](java/app-encryption)
* [.NET](csharp/AppEncryption)
* [Go](go/appencryption)
* [Rust (`memguard-rs`)](rust/memguard)
* [Service Layer (gRPC)](/server)

### Derived Projects

[Asherah-Cobhan](https://github.com/godaddy/asherah-cobhan) implementations built atop the [Asherah SDK for Go](go/appencryption).

* [Node.JS](https://github.com/godaddy/asherah-node)
* [Ruby](https://github.com/godaddy/asherah-ruby)
* [Python](https://github.com/godaddy/asherah-python)

### Feature Support

* AWS KMS Support
* RDBMS Metastore
* DynamoDB Metastore
* Session caching
* Encrypt/Decrypt pattern
* Store/Load pattern

---

### `memguard-rs` Feature Support

The Rust port (`memguard-rs`) focuses on the core secure memory primitives provided by the Go `memguard` library. Its features include:

*   **Secure Buffer (`Buffer`)**:
    *   Memory allocation with guard pages (via `memcall-rs`).
    *   Memory locking (`mlock`/`VirtualLock` via `memcall-rs`) to prevent swapping.
    *   Automatic and explicit memory wiping (`Drop` trait, `destroy()` method).
    *   Controlled mutability (`freeze()`/`melt()`).
    *   Canary values to detect overflows.
*   **Encrypted Container (`Enclave`)**:
    *   AEAD encryption (ChaCha20-Poly1305) for data stored in Buffers.
    *   Encryption keys managed by an internal `Coffer` system.
*   **Secure Key Coffer (`Coffer`)**:
    *   Internal mechanism for managing the master encryption key.
    *   Periodic, automatic re-keying of the Coffer's internal representation.
*   **Streaming Encryption (`Stream`)**:
    *   `std::io::Read` and `std::io::Write` interface for handling large sensitive data in encrypted chunks.
*   **Signal Handling**:
    *   Functions (`catch_interrupt`, `catch_signal`) for graceful cleanup of sensitive data on program termination signals (Unix-like systems).
*   **Emergency Purge**:
    *   `purge()` function to wipe all tracked sensitive data and reset encryption keys.
*   **Safe Termination**:
    *   `safe_exit()` and `safe_panic()` to ensure data is wiped before program termination.
*   **Core Dump Disabling**:
    *   Attempts to disable core dumps on Unix-like systems to prevent sensitive data leakage. Windows behavior relies on OS settings.

`memguard-rs` does *not* directly implement higher-level features of the broader Asherah SDK like:
*   AWS KMS Support
*   RDBMS Metastore
*   DynamoDB Metastore
*   Session Caching (application-level)
*   Store/Load pattern (application-level persistence abstraction)

These features can be built on top of `memguard-rs` by an application if needed.

## Contributing

All contributors and contributions are welcome! Please see our [contributing docs](CONTRIBUTING.md) for more
information.
