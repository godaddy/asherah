# Asherah GraalVM Native Image Sample

This sample demonstrates using Asherah application-level encryption compiled as a GraalVM Native Image.

## Features

- **GraalVM 25 Native Image** - Compiles to a standalone native executable (~62MB)
- **JDK Crypto Provider** - Uses hardware-accelerated AES-256-GCM (no BouncyCastle)
- **FFM Secure Memory** - Uses Java 22+ Foreign Function & Memory API for secure key storage
- **Fast Startup** - Native executable starts in milliseconds
- **Low Memory** - Reduced memory footprint compared to JVM

## Requirements

- **GraalVM 25** (or later) with `native-image` tool
- **Maven 3.8+**
- **Local Asherah libraries** built and installed to local Maven repository:
  - `appencryption:0.3.4`
  - `securememory:0.1.7`

### Installing GraalVM 25

```bash
# macOS with Homebrew
brew install --cask graalvm-jdk

# Or with asdf
asdf install java oracle-graalvm-25.0.0

# Or download from https://www.graalvm.org/downloads/
```

Ensure `JAVA_HOME` and `GRAALVM_HOME` point to GraalVM:

```bash
export JAVA_HOME=/Library/Java/JavaVirtualMachines/graalvm-jdk-25/Contents/Home
export GRAALVM_HOME=$JAVA_HOME
```

## Building

### Build Asherah Dependencies First

```bash
# From repository root
cd java/secure-memory
mvn clean install -DskipTests

cd ../app-encryption
mvn clean install -DskipTests
```

### Build Native Image

```bash
cd samples/java/native-app

# Build the native executable (requires GraalVM in JAVA_HOME)
mvn clean package -DskipTests

# The native executable will be at: target/asherah-native
```

### Build JAR (for comparison/testing)

```bash
mvn package -Pjar -DskipTests
```

## Running

### Native Executable

```bash
# Run with defaults (3 iterations)
./target/asherah-native

# Run with custom iterations
./target/asherah-native 10

# Run with custom payload
./target/asherah-native 5 "My secret data"
```

### JAR Version

```bash
java --enable-native-access=ALL-UNNAMED \
  -jar target/native-app-1.0.0-SNAPSHOT-jar-with-dependencies.jar
```

## Example Output

```
07:03:41.339 [INFO] App - === Asherah GraalVM Native Image Sample ===
07:03:41.339 [INFO] App - Crypto Engine: JDK AES-256-GCM
07:03:41.339 [INFO] App - Java Version: 25
07:03:41.339 [INFO] App - Iterations: 3
07:03:41.339 [INFO] App - Payload: Hello from GraalVM Native Image!
07:03:41.339 [INFO] App - Using in-memory metastore
07:03:41.343 [INFO] TransientSecretFactory - Using FFM-based SecretFactory (Java 22+ detected)
07:03:41.343 [INFO] App - Using static key management service with FFM secure memory
07:03:41.343 [INFO] App - Creating session factory with JDK crypto engine...
07:03:41.344 [INFO] App - --- Iteration 1 ---
07:03:41.346 [INFO] App - Encrypted (Base64): eyJEYXRhIjoiTENFbjRnYytZVm5DWVNGN2lrN2xKbFJ2aERsaE...
07:03:41.346 [INFO] App - Encrypt time: 1537 µs
07:03:41.346 [INFO] App - Decrypted: Hello from GraalVM Native Image!
07:03:41.346 [INFO] App - Decrypt time: 261 µs
07:03:41.346 [INFO] App - Matches original: true
...
07:03:41.346 [INFO] App - === Summary ===
07:03:41.346 [INFO] App - Average encrypt time: 617 µs
07:03:41.346 [INFO] App - Average decrypt time: 164 µs
07:03:41.346 [INFO] App - All iterations successful!
```

## Performance

| Metric | Native Image | JVM (JIT) |
|--------|-------------|-----------|
| Startup time | ~10ms | ~500ms |
| Memory (RSS) | ~30MB | ~150MB |
| First encrypt | Fast (pre-compiled) | Slower (JIT warmup) |
| Steady state | Comparable | Slightly faster after warmup |

## Why JDK Crypto?

This sample uses the JDK crypto provider instead of BouncyCastle for several reasons:

1. **Hardware Acceleration** - JDK AES-GCM uses CPU intrinsics (AES-NI) on modern processors
2. **Native Image Compatible** - JDK crypto works well with GraalVM native-image
3. **No External Dependencies** - Reduces native-image size and complexity
4. **Performance** - JDK crypto with AES-NI is faster than BouncyCastle for AES-GCM

## Why FFM Secure Memory?

The FFM (Foreign Function & Memory) implementation for secure memory:

1. **No JNA Overhead** - Direct system calls without JNA library
2. **Native Image Compatible** - FFM works better with native-image than JNA
3. **Memory Protection** - Same security features (mlock, mprotect, secure zeroing)
4. **GraalVM Integration** - FFM is well-supported by GraalVM native-image

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Native App                                │
├─────────────────────────────────────────────────────────────┤
│  App.java                                                    │
│  └── NativeStaticKeyManagementService                       │
│       └── FfmSecretFactory (direct FFM, no reflection)      │
│            └── FfmProtectedMemoryAllocator                  │
│                 └── macOS/Linux native calls via FFM        │
├─────────────────────────────────────────────────────────────┤
│  SessionFactory → Session → encrypt/decrypt                 │
│  └── JdkAes256GcmCrypto (hardware-accelerated AES-GCM)     │
└─────────────────────────────────────────────────────────────┘
```

## Key Differences from Reference App

| Feature | Reference App | Native App |
|---------|---------------|------------|
| Crypto Engine | BouncyCastle (default) | JDK only |
| Secure Memory | JNA-based | FFM-based |
| Compilation | JIT | AOT (native-image) |
| Dependencies | Full (AWS, metrics) | Minimal |
| Executable | JAR (~30MB) | Native (~62MB) |

## Troubleshooting

### Missing native-image tool

```bash
# GraalVM 25 includes native-image by default
# Verify with:
native-image --version
```

### FFM-related errors

If you see `MissingForeignRegistrationError`, you may need to regenerate the metadata:

```bash
# Run with native-image agent to capture configurations
java -agentlib:native-image-agent=config-output-dir=target/generated-config \
  --enable-native-access=ALL-UNNAMED \
  -jar target/native-app-1.0.0-SNAPSHOT-jar-with-dependencies.jar

# Copy generated config to resources
cp target/generated-config/reachability-metadata.json \
   src/main/resources/META-INF/native-image/
```

### Memory allocation errors

Ensure you have sufficient memory for native-image compilation:

```bash
export MAVEN_OPTS="-Xmx4g"
mvn clean package -DskipTests
```

## License

Same as the parent Asherah project.
