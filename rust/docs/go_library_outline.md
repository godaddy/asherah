# Comprehensive Outline of Go Libraries in Asherah

This document provides a detailed outline of the structures, interfaces, functions, and other components in the Go implementation of Asherah, organized by library and package.

## 1. SecureMemory

### 1.1. Core Interfaces

#### `Secret` Interface
- `WithBytes(operation func([]byte) error) error`: Execute operation with secret bytes
- `WithBytesFunc(operation func([]byte) (interface{}, error)) (interface{}, error)`: Execute operation with return value
- `Reader() io.Reader`: Get a reader for the secret
- `Close()`: Close the secret and wipe memory
- `Destroyed() bool`: Check if secret has been destroyed

#### `SecretFactory` Interface
- `New(data []byte) (Secret, error)`: Create new secret from bytes
- `CreateRandom(size int) (Secret, error)`: Create random secret of given size

### 1.2. ProtectedMemory Implementation

#### `ProtectedMemorySecret` Struct
- Fields:
  - `data unsafe.Pointer`: Pointer to protected memory
  - `size int`: Size of allocated memory
  - `destroyed bool`: Flag indicating if memory is destroyed
  - `mutex sync.RWMutex`: Lock for thread safety

#### `ProtectedMemorySecretFactory` Struct
- Method implementation for `SecretFactory` interface

#### Memory Protection Functions
- `AllocateProtected(size int) (unsafe.Pointer, error)`: Allocate protected memory
- `FreeProtected(pointer unsafe.Pointer, size int) error`: Free protected memory
- `ProtectMemory(pointer unsafe.Pointer, size int, access MemoryProtection) error`: Change memory protection
- `LockMemory(pointer unsafe.Pointer, size int) error`: Lock memory to prevent swapping
- `UnlockMemory(pointer unsafe.Pointer, size int) error`: Unlock memory

### 1.3. Memguard Implementation

#### `MemguardSecret` Struct
- Fields:
  - `lockedBuffer *memguard.LockedBuffer`: Underlying protected buffer
  - `destroyed bool`: Flag indicating if memory is destroyed
  - `mutex sync.RWMutex`: Lock for thread safety

#### `MemguardSecretFactory` Struct
- Method implementation for `SecretFactory` interface

### 1.4. Error Types
- `MemoryProtectionError`: Error with memory protection operations
- `SecureMemoryError`: Base secure memory error
- `ResourceLimitError`: Error when resource limits are exceeded
- `OperationFailedError`: Error when a security operation fails

## 2. Memguard

### 2.1. Core Components

#### `LockedBuffer` Struct
- Fields:
  - `bytes []byte`: Underlying byte array
  - `master *key`: Master encryption key
  - `buf *buffer`: Internal buffer reference
  - `mutable bool`: Flag indicating if buffer is mutable

#### `Enclave` Package
- `New(buf *LockedBuffer) (*Enclave, error)`: Create new enclave
- `Seal(buf *LockedBuffer) ([]byte, error)`: Seal buffer into enclave
- `Open(ciphertext []byte) (*LockedBuffer, error)`: Open enclave to buffer

#### Memory Management
- `Alloc(size int) (*LockedBuffer, error)`: Allocate locked buffer
- `AllocFromBytes(buf []byte) (*LockedBuffer, error)`: Allocate from existing bytes
- `Wipe(buf []byte)`: Securely wipe memory
- `Lock(buf []byte) error`: Lock memory to prevent swapping
- `Unlock(buf []byte) error`: Unlock memory

### 2.2. Stream API

#### `Stream` Struct
- Fields:
  - `buffer *LockedBuffer`: Underlying locked buffer
  - `offset int`: Current read offset
  - `eof bool`: Flag indicating end of stream

#### Stream Functions
- `NewStream() (*Stream, error)`: Create new empty stream
- `NewStreamFromBytes(buf []byte) (*Stream, error)`: Create stream from bytes
- `Write(p []byte) (int, error)`: Write data to stream
- `Read(p []byte) (int, error)`: Read data from stream
- `Flush() (*LockedBuffer, error)`: Flush stream to locked buffer
- `Close() error`: Close stream and wipe memory

### 2.3. Signal Handling
- `CatchSignal(signals ...os.Signal)`: Catch specific signals
- `CatchInterrupt()`: Catch interrupt signal (SIGINT)
- `SafeExit(code int)`: Exit safely after wiping memory
- `SafePanic(v interface{})`: Panic safely after wiping memory

## 3. Memcall

### 3.1. Core Functions
- `Alloc(size int) ([]byte, error)`: Allocate memory
- `Free(ptr []byte) error`: Free memory
- `Protect(ptr []byte, readOnly bool) error`: Set memory protection
- `Unprotect(ptr []byte) error`: Remove memory protection
- `Lock(ptr []byte) error`: Lock memory to prevent swapping
- `Unlock(ptr []byte) error`: Unlock memory
- `DisableCoreDumps() error`: Disable core dumps

### 3.2. Platform-Specific Implementations
- `memcall_unix.go`: Unix-specific memory functions
- `memcall_windows.go`: Windows-specific memory functions
- `memcall_darwin.go`: macOS-specific memory functions
- `memcall_freebsd.go`: FreeBSD-specific memory functions
- `memcall_openbsd.go`: OpenBSD-specific memory functions
- `memcall_netbsd.go`: NetBSD-specific memory functions
- `memcall_solaris.go`: Solaris-specific memory functions

## 4. AppEncryption

### 4.1. Core Interfaces

#### `Partition` Interface
- `GetSystemKeyId() string`: Get system key ID
- `GetIntermediateKeyId(timestamp time.Time) string`: Get intermediate key ID for timestamp
- `IsValidIntermediateKeyId(keyId string) bool`: Validate intermediate key ID

#### `Session` Interface
- `Encrypt(data []byte) ([]byte, error)`: Encrypt data
- `Decrypt(data []byte) ([]byte, error)`: Decrypt data
- `Close() error`: Close session and clean up

#### `Metastore` Interface
- `Load(keyId string, created time.Time) (*EnvelopeKeyRecord, error)`: Load key record
- `LoadLatest(keyId string) (*EnvelopeKeyRecord, error)`: Load latest key record
- `Store(keyRecord *EnvelopeKeyRecord) (bool, error)`: Store key record

#### `KeyManagementService` Interface
- `GenerateDataKey(parentKeyId string) ([]byte, []byte, error)`: Generate data key
- `DecryptDataKey(encryptedKey []byte, parentKeyId string) ([]byte, error)`: Decrypt data key

### 4.2. Implementations

#### Partition Implementations
- `DefaultPartition`: Basic partition implementation
- `SuffixedPartition`: Partition with suffix for multi-region

#### Session Implementations
- `SessionBytes`: Session for byte array encryption/decryption
- `SessionJSON`: Session for JSON object encryption/decryption

#### Metastore Implementations
- `DynamoDBMetastore`: AWS DynamoDB-based metastore
- `InMemoryMetastore`: In-memory metastore for testing
- `SQLMetastore`: SQL database metastore
- `AdoMetastore`: ADO.NET metastore for .NET applications

#### KMS Implementations
- `AwsKeyManagementService`: AWS KMS implementation
- `StaticKeyManagementService`: Static KMS for testing

### 4.3. Caching

#### `KeyCache` Interface
- `Contains(key interface{}) bool`: Check if key exists in cache
- `Get(key interface{}) (interface{}, bool)`: Get value from cache
- `Put(key interface{}, value interface{}) bool`: Put value in cache
- `Remove(key interface{}) bool`: Remove key from cache

#### Cache Implementations
- `LRU`: Least Recently Used cache
- `LFU`: Least Frequently Used cache
- `TLFU`: Two-Level Frequency and Usage cache
- `TinyLFU`: TinyLFU sketch for admission policy

### 4.4. Crypto

#### `AeadCrypto` Interface
- `Encrypt(plaintext, additionalData []byte) ([]byte, error)`: Encrypt with AEAD
- `Decrypt(ciphertext, additionalData []byte) ([]byte, error)`: Decrypt with AEAD

#### Crypto Implementations
- `AES256GCM`: AES-256-GCM implementation
- `EnvelopeCrypto`: Envelope encryption implementation

#### Crypto Policy
- `CryptoPolicy` interface: Define key rotation and expiration
- `BasicExpiringCryptoPolicy`: Basic implementation with expiration
- `NeverExpiredCryptoPolicy`: Policy where keys never expire

### 4.5. Session Factory

#### `SessionFactory` Struct
- Fields:
  - `metastore Metastore`: Metastore for key storage
  - `kms KeyManagementService`: KMS for key management
  - `crypto AeadCrypto`: Crypto implementation
  - `policy CryptoPolicy`: Crypto policy for rotation
  - `cache KeyCache`: Cache for keys
  - `sessionCache SessionCache`: Cache for sessions

#### Factory Methods
- `GetSession(id string) (Session, error)`: Get session for ID
- `GetSessionBytes(id string) (Session, error)`: Get bytes session
- `GetSessionJSON(id string) (Session, error)`: Get JSON session
- `Close() error`: Close factory and clean up

### 4.6. AWS Plugins

#### AWS v1 KMS
- `AwsKmsClientFactory`: Factory for AWS KMS clients
- `AwsKeyManagementServiceImpl`: AWS KMS implementation using v1 SDK

#### AWS v1 DynamoDB
- `DynamoDBMetastoreImpl`: DynamoDB metastore using v1 SDK
- `DynamoDBClient`: Interface for DynamoDB operations

#### AWS v2 KMS
- `AwsKmsBuilder`: Builder for AWS KMS clients
- `AwsKeyManagementServiceImpl`: AWS KMS implementation using v2 SDK

#### AWS v2 DynamoDB
- `DynamoDBMetastoreImpl`: DynamoDB metastore using v2 SDK
- `DynamoDBClient`: Interface for DynamoDB operations

## 5. Testing and Examples

### 5.1. Integration Tests
- `MultiThreadedTests`: Tests for multi-threaded usage
- `CrossPartitionTests`: Tests for cross-partition operations
- `MetastoreInteractionTests`: Tests for metastore interactions
- `SessionCacheTests`: Tests for session caching

### 5.2. Performance Tests
- `BenchmarkTests`: Performance benchmarks
- `TraceAnalysis`: Analysis of performance traces
- `CachePerformance`: Benchmark of caching strategies

### 5.3. Cross-Language Tests
- `CrossLanguageEncryptTests`: Tests for cross-language encryption
- `CrossLanguageDecryptTests`: Tests for cross-language decryption

### 5.4. Examples
- `ReferenceApp`: Reference application showing usage
- `AwsLambda`: Example AWS Lambda integration
- `WebApplication`: Example web application integration