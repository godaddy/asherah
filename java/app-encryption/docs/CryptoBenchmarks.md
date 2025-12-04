# Crypto Engine Benchmark Results

This document captures benchmark results comparing **BouncyCastle** and **JDK** cryptographic implementations across different Java versions.

## Test Environment

- **Machine**: Apple Silicon (ARM64)
- **Date**: December 2025
- **Benchmark**: 10,000 iterations per test with 1,000 warmup iterations
- **Algorithm**: AES-256-GCM

### Java Versions Tested

| Version | Distribution | Build |
|---------|--------------|-------|
| Java 17 | Temurin | 17.0.16+8 |
| Java 21 | Temurin | 21.0.7+6-LTS |
| Java 25 | Oracle GraalVM | 25+37.1-LTS |

---

## Summary

**JDK crypto with Java 21+ is the clear winner**, offering up to **10x better performance** than BouncyCastle for larger payloads.

| Configuration | 4KB Round-trip | Relative Performance |
|---------------|----------------|---------------------|
| BouncyCastle + Java 17 | 179.35 µs | 1.0x (baseline) |
| BouncyCastle + Java 21 | 77.93 µs | 2.3x faster |
| BouncyCastle + Java 25 | 80.13 µs | 2.2x faster |
| JDK + Java 17 | 116.74 µs | 1.5x faster |
| **JDK + Java 21** | **8.62 µs** | **20.8x faster** |
| **JDK + Java 25** | **8.04 µs** | **22.3x faster** |

---

## Detailed Results

### Encryption Performance

Time to encrypt payload (microseconds, lower is better).

| Payload Size | BC (Java 17) | BC (Java 21) | BC (Java 25) | JDK (Java 17) | JDK (Java 21) | JDK (Java 25) |
|--------------|--------------|--------------|--------------|---------------|---------------|---------------|
| 64 bytes | 9.78 µs | 6.71 µs | 6.81 µs | 7.85 µs | 3.29 µs | **3.29 µs** |
| 256 bytes | 13.37 µs | 8.56 µs | 8.41 µs | 7.55 µs | 3.31 µs | **3.28 µs** |
| 1024 bytes | 31.81 µs | 14.18 µs | 15.26 µs | 18.47 µs | 4.13 µs | **3.23 µs** |
| 4096 bytes | 96.68 µs | 38.94 µs | 41.43 µs | 58.27 µs | 4.55 µs | **4.10 µs** |

### Decryption Performance

Time to decrypt payload (microseconds, lower is better).

| Payload Size | BC (Java 17) | BC (Java 21) | BC (Java 25) | JDK (Java 17) | JDK (Java 21) | JDK (Java 25) |
|--------------|--------------|--------------|--------------|---------------|---------------|---------------|
| 64 bytes | 17.07 µs | 8.68 µs | 10.37 µs | 12.95 µs | 8.87 µs | 10.77 µs |
| 256 bytes | 17.18 µs | 9.40 µs | 9.95 µs | 12.62 µs | **4.07 µs** | 7.58 µs |
| 1024 bytes | 29.70 µs | 14.74 µs | 15.11 µs | 18.76 µs | **3.56 µs** | 10.12 µs |
| 4096 bytes | 89.11 µs | 38.24 µs | 38.96 µs | 54.50 µs | **4.23 µs** | 4.37 µs |

### Round-Trip Performance (Encrypt + Decrypt)

Time to encrypt and then decrypt payload (microseconds, lower is better).

| Payload Size | BC (Java 17) | BC (Java 21) | BC (Java 25) | JDK (Java 17) | JDK (Java 21) | JDK (Java 25) |
|--------------|--------------|--------------|--------------|---------------|---------------|---------------|
| 64 bytes | 24.83 µs | 14.12 µs | 14.97 µs | 10.43 µs | 6.74 µs | **6.52 µs** |
| 256 bytes | 29.05 µs | 16.62 µs | 17.18 µs | 14.60 µs | 7.02 µs | **6.47 µs** |
| 1024 bytes | 59.97 µs | 29.18 µs | 29.52 µs | 35.54 µs | 7.12 µs | **6.52 µs** |
| 4096 bytes | 179.35 µs | 77.93 µs | 80.13 µs | 116.74 µs | 8.62 µs | **8.04 µs** |

---

## Performance Improvements by Java Version

### BouncyCastle Improvements

| Upgrade Path | Encryption (4KB) | Decryption (4KB) | Round-trip (4KB) |
|--------------|------------------|------------------|------------------|
| Java 17 → 21 | **2.5x faster** | **2.3x faster** | **2.3x faster** |
| Java 21 → 25 | ~same | ~same | ~same |
| Java 17 → 25 | **2.3x faster** | **2.3x faster** | **2.2x faster** |

### JDK Crypto Improvements

| Upgrade Path | Encryption (4KB) | Decryption (4KB) | Round-trip (4KB) |
|--------------|------------------|------------------|------------------|
| Java 17 → 21 | **12.8x faster** | **12.9x faster** | **13.5x faster** |
| Java 21 → 25 | 1.1x faster | ~same | 1.1x faster |
| Java 17 → 25 | **14.2x faster** | **12.5x faster** | **14.5x faster** |

---

## JDK vs BouncyCastle Comparison

### Speedup Factor (JDK over BouncyCastle)

| Payload Size | Java 17 | Java 21 | Java 25 |
|--------------|---------|---------|---------|
| 64 bytes | 1.25x | 2.04x | 2.07x |
| 256 bytes | 1.77x | 2.59x | 2.57x |
| 1024 bytes | 1.72x | 3.43x | 4.73x |
| 4096 bytes | 1.66x | 8.56x | **10.10x** |

> **Note**: The performance advantage of JDK crypto increases with payload size due to better vectorization and hardware acceleration.

---

## Analysis

### Why JDK Crypto is Faster

1. **Hardware Acceleration (AES-NI)**: JDK's SunJCE provider leverages CPU AES-NI instructions directly through JVM intrinsics.

2. **Vectorization**: Java 21+ includes improved vector operations that benefit GCM's GHASH computation.

3. **JIT Optimization**: The JDK crypto code paths are heavily optimized by the JIT compiler over time.

4. **Zero-Copy Operations**: JDK implementation minimizes memory allocations and copies.

### Why BouncyCastle is Slower

1. **Pure Java Implementation**: BouncyCastle's GCM implementation is written in pure Java without direct hardware intrinsics.

2. **Portable Design**: Designed for maximum compatibility across platforms, trading performance for portability.

3. **Additional Abstraction**: More abstraction layers between the API and actual crypto operations.

### Java Version Impact

- **Java 17 → 21**: Major crypto performance improvements due to:
  - Enhanced intrinsics for AES-GCM
  - Better vectorization support
  - Improved JIT compilation for crypto workloads

- **Java 21 → 25**: Incremental improvements, mainly:
  - GraalVM's advanced JIT optimizations
  - Slightly better memory handling

---

## Recommendations

### By Use Case

| Use Case | Recommended Configuration |
|----------|--------------------------|
| **Maximum Performance** | JDK Crypto + Java 21/25 |
| **GraalVM Native Image** | JDK Crypto + Java 21 |
| **Legacy Java 17 Required** | JDK Crypto (still 1.5x faster than BC) |
| **Maximum Compatibility** | BouncyCastle (works on all JVMs) |
| **FIPS Compliance** | BouncyCastle FIPS or platform-specific |

### Migration Path

1. **Immediate Win**: Switch from BouncyCastle to JDK Crypto
   - No Java version change needed
   - 1.5-2x performance improvement on Java 17

2. **Best ROI**: Upgrade to Java 21 LTS
   - 13x performance improvement for JDK crypto
   - 2.3x improvement even for BouncyCastle
   - Long-term support until 2029

3. **Future-Proof**: Consider Java 25 (when LTS)
   - New crypto APIs (KDF, PEM encoding)
   - Post-quantum crypto readiness
   - Best overall performance

---

## How to Run Benchmarks

### Quick Benchmark (JUnit)

```bash
cd java/app-encryption
mvn test -Dtest=CryptoEngineBenchmarkTest -q
```

### Full JMH Benchmark

```bash
cd java/app-encryption
mvn test-compile exec:java \
  -Dexec.mainClass="com.godaddy.asherah.crypto.engine.CryptoEngineBenchmark" \
  -Dexec.classpathScope="test"
```

### Benchmark with Specific Java Version

```bash
# Using asdf
export JAVA_HOME=$(asdf where java temurin-21.0.7+6.0.LTS)
export PATH=$JAVA_HOME/bin:$PATH
mvn test -Dtest=CryptoEngineBenchmarkTest -q

# Using SDKMAN
sdk use java 21.0.2-tem
mvn test -Dtest=CryptoEngineBenchmarkTest -q
```

---

## Appendix: Raw Benchmark Output

### Java 17 (Temurin 17.0.16+8)

```
=== Encryption Benchmark ===
Payload         BouncyCastle             JDK       JDK vs BC
------------------------------------------------------------
64 bytes             9.78 µs         7.85 µs         1.25x
256 bytes           13.37 µs         7.55 µs         1.77x
1024 bytes          31.81 µs        18.47 µs         1.72x
4096 bytes          96.68 µs        58.27 µs         1.66x

=== Decryption Benchmark ===
Payload         BouncyCastle             JDK       JDK vs BC
------------------------------------------------------------
64 bytes            17.07 µs        12.95 µs         1.32x
256 bytes           17.18 µs        12.62 µs         1.36x
1024 bytes          29.70 µs        18.76 µs         1.58x
4096 bytes          89.11 µs        54.50 µs         1.63x

=== Round-Trip Benchmark ===
Payload         BouncyCastle             JDK       JDK vs BC
------------------------------------------------------------
64 bytes            24.83 µs        10.43 µs         2.38x
256 bytes           29.05 µs        14.60 µs         1.99x
1024 bytes          59.97 µs        35.54 µs         1.69x
4096 bytes         179.35 µs       116.74 µs         1.54x
```

### Java 21 (Temurin 21.0.7+6-LTS)

```
=== Encryption Benchmark ===
Payload         BouncyCastle             JDK       JDK vs BC
------------------------------------------------------------
64 bytes             6.71 µs         3.29 µs         2.04x
256 bytes            8.56 µs         3.31 µs         2.59x
1024 bytes          14.18 µs         4.13 µs         3.43x
4096 bytes          38.94 µs         4.55 µs         8.56x

=== Decryption Benchmark ===
Payload         BouncyCastle             JDK       JDK vs BC
------------------------------------------------------------
64 bytes             8.68 µs         8.87 µs         0.98x
256 bytes            9.40 µs         4.07 µs         2.31x
1024 bytes          14.74 µs         3.56 µs         4.14x
4096 bytes          38.24 µs         4.23 µs         9.05x

=== Round-Trip Benchmark ===
Payload         BouncyCastle             JDK       JDK vs BC
------------------------------------------------------------
64 bytes            14.12 µs         6.74 µs         2.10x
256 bytes           16.62 µs         7.02 µs         2.37x
1024 bytes          29.18 µs         7.12 µs         4.10x
4096 bytes          77.93 µs         8.62 µs         9.04x
```

### Java 25 (Oracle GraalVM 25+37.1-LTS)

```
=== Encryption Benchmark ===
Payload         BouncyCastle             JDK       JDK vs BC
------------------------------------------------------------
64 bytes             6.81 µs         3.29 µs         2.07x
256 bytes            8.41 µs         3.28 µs         2.57x
1024 bytes          15.26 µs         3.23 µs         4.73x
4096 bytes          41.43 µs         4.10 µs        10.10x

=== Decryption Benchmark ===
Payload         BouncyCastle             JDK       JDK vs BC
------------------------------------------------------------
64 bytes            10.37 µs        10.77 µs         0.96x
256 bytes            9.95 µs         7.58 µs         1.31x
1024 bytes          15.11 µs        10.12 µs         1.49x
4096 bytes          38.96 µs         4.37 µs         8.91x

=== Round-Trip Benchmark ===
Payload         BouncyCastle             JDK       JDK vs BC
------------------------------------------------------------
64 bytes            14.97 µs         6.52 µs         2.29x
256 bytes           17.18 µs         6.47 µs         2.66x
1024 bytes          29.52 µs         6.52 µs         4.53x
4096 bytes          80.13 µs         8.04 µs         9.96x
```

---

## Related Files

- [`JdkAes256GcmCrypto.java`](../src/main/java/com/godaddy/asherah/crypto/engine/jdk/JdkAes256GcmCrypto.java) - JDK crypto implementation
- [`BouncyAes256GcmCrypto.java`](../src/main/java/com/godaddy/asherah/crypto/engine/bouncycastle/BouncyAes256GcmCrypto.java) - BouncyCastle implementation
- [`CryptoEngineType.java`](../src/main/java/com/godaddy/asherah/crypto/engine/CryptoEngineType.java) - Provider selection enum
- [`CryptoEngineBenchmarkTest.java`](../src/test/java/com/godaddy/asherah/crypto/engine/CryptoEngineBenchmarkTest.java) - Benchmark tests

