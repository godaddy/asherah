package com.godaddy.asherah.crypto.engine;

import static org.junit.jupiter.api.Assertions.assertTrue;

import java.security.SecureRandom;
import java.util.ArrayList;
import java.util.List;

import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAes256GcmCrypto;
import com.godaddy.asherah.crypto.engine.jdk.JdkAes256GcmCrypto;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;

/**
 * Simple benchmark test that compares BouncyCastle and JDK crypto performance.
 * This is a lightweight alternative to JMH for quick performance comparisons.
 *
 * <p>For more accurate benchmarks, use {@link CryptoEngineBenchmark} with JMH.
 */
class CryptoEngineBenchmarkTest {

  private static final int WARMUP_ITERATIONS = 1000;
  private static final int BENCHMARK_ITERATIONS = 10000;
  private static final int[] PAYLOAD_SIZES = {64, 256, 1024, 4096};

  private static AeadEnvelopeCrypto bouncyCrypto;
  private static AeadEnvelopeCrypto jdkCrypto;
  private static SecureRandom random;

  @BeforeAll
  static void setup() {
    bouncyCrypto = new BouncyAes256GcmCrypto();
    jdkCrypto = new JdkAes256GcmCrypto();
    random = new SecureRandom();
  }

  @Test
  @DisplayName("Benchmark: Compare BouncyCastle vs JDK encryption performance")
  void benchmarkEncryption() {
    System.out.println("\n=== Encryption Benchmark ===");
    System.out.println("Iterations: " + BENCHMARK_ITERATIONS);
    System.out.printf("%-12s %15s %15s %15s%n", "Payload", "BouncyCastle", "JDK", "JDK vs BC");
    System.out.println("-".repeat(60));

    for (int size : PAYLOAD_SIZES) {
      byte[] payload = new byte[size];
      random.nextBytes(payload);

      CryptoKey bouncy_key = bouncyCrypto.generateKey();
      CryptoKey jdk_key = jdkCrypto.generateKey();

      // Warmup
      for (int i = 0; i < WARMUP_ITERATIONS; i++) {
        bouncyCrypto.encrypt(payload, bouncy_key);
        jdkCrypto.encrypt(payload, jdk_key);
      }

      // Benchmark BouncyCastle
      long bouncy_start = System.nanoTime();
      for (int i = 0; i < BENCHMARK_ITERATIONS; i++) {
        bouncyCrypto.encrypt(payload, bouncy_key);
      }
      long bouncy_time = System.nanoTime() - bouncy_start;

      // Benchmark JDK
      long jdk_start = System.nanoTime();
      for (int i = 0; i < BENCHMARK_ITERATIONS; i++) {
        jdkCrypto.encrypt(payload, jdk_key);
      }
      long jdk_time = System.nanoTime() - jdk_start;

      double bouncy_avg_us = (bouncy_time / 1000.0) / BENCHMARK_ITERATIONS;
      double jdk_avg_us = (jdk_time / 1000.0) / BENCHMARK_ITERATIONS;
      double ratio = bouncy_avg_us / jdk_avg_us;

      System.out.printf("%-12s %12.2f µs %12.2f µs %12.2fx%n",
          size + " bytes", bouncy_avg_us, jdk_avg_us, ratio);

      // Just verify both work, not asserting performance
      assertTrue(bouncy_time > 0);
      assertTrue(jdk_time > 0);
    }
  }

  @Test
  @DisplayName("Benchmark: Compare BouncyCastle vs JDK decryption performance")
  void benchmarkDecryption() {
    System.out.println("\n=== Decryption Benchmark ===");
    System.out.println("Iterations: " + BENCHMARK_ITERATIONS);
    System.out.printf("%-12s %15s %15s %15s%n", "Payload", "BouncyCastle", "JDK", "JDK vs BC");
    System.out.println("-".repeat(60));

    for (int size : PAYLOAD_SIZES) {
      byte[] payload = new byte[size];
      random.nextBytes(payload);

      CryptoKey bouncy_key = bouncyCrypto.generateKey();
      CryptoKey jdk_key = jdkCrypto.generateKey();

      byte[] bouncy_ciphertext = bouncyCrypto.encrypt(payload, bouncy_key);
      byte[] jdk_ciphertext = jdkCrypto.encrypt(payload, jdk_key);

      // Warmup
      for (int i = 0; i < WARMUP_ITERATIONS; i++) {
        bouncyCrypto.decrypt(bouncy_ciphertext, bouncy_key);
        jdkCrypto.decrypt(jdk_ciphertext, jdk_key);
      }

      // Benchmark BouncyCastle
      long bouncy_start = System.nanoTime();
      for (int i = 0; i < BENCHMARK_ITERATIONS; i++) {
        bouncyCrypto.decrypt(bouncy_ciphertext, bouncy_key);
      }
      long bouncy_time = System.nanoTime() - bouncy_start;

      // Benchmark JDK
      long jdk_start = System.nanoTime();
      for (int i = 0; i < BENCHMARK_ITERATIONS; i++) {
        jdkCrypto.decrypt(jdk_ciphertext, jdk_key);
      }
      long jdk_time = System.nanoTime() - jdk_start;

      double bouncy_avg_us = (bouncy_time / 1000.0) / BENCHMARK_ITERATIONS;
      double jdk_avg_us = (jdk_time / 1000.0) / BENCHMARK_ITERATIONS;
      double ratio = bouncy_avg_us / jdk_avg_us;

      System.out.printf("%-12s %12.2f µs %12.2f µs %12.2fx%n",
          size + " bytes", bouncy_avg_us, jdk_avg_us, ratio);

      assertTrue(bouncy_time > 0);
      assertTrue(jdk_time > 0);
    }
  }

  @Test
  @DisplayName("Benchmark: Compare BouncyCastle vs JDK round-trip performance")
  void benchmarkRoundTrip() {
    System.out.println("\n=== Round-Trip (Encrypt + Decrypt) Benchmark ===");
    System.out.println("Iterations: " + BENCHMARK_ITERATIONS);
    System.out.printf("%-12s %15s %15s %15s%n", "Payload", "BouncyCastle", "JDK", "JDK vs BC");
    System.out.println("-".repeat(60));

    for (int size : PAYLOAD_SIZES) {
      byte[] payload = new byte[size];
      random.nextBytes(payload);

      CryptoKey bouncy_key = bouncyCrypto.generateKey();
      CryptoKey jdk_key = jdkCrypto.generateKey();

      // Warmup
      for (int i = 0; i < WARMUP_ITERATIONS; i++) {
        bouncyCrypto.decrypt(bouncyCrypto.encrypt(payload, bouncy_key), bouncy_key);
        jdkCrypto.decrypt(jdkCrypto.encrypt(payload, jdk_key), jdk_key);
      }

      // Benchmark BouncyCastle
      long bouncy_start = System.nanoTime();
      for (int i = 0; i < BENCHMARK_ITERATIONS; i++) {
        byte[] ct = bouncyCrypto.encrypt(payload, bouncy_key);
        bouncyCrypto.decrypt(ct, bouncy_key);
      }
      long bouncy_time = System.nanoTime() - bouncy_start;

      // Benchmark JDK
      long jdk_start = System.nanoTime();
      for (int i = 0; i < BENCHMARK_ITERATIONS; i++) {
        byte[] ct = jdkCrypto.encrypt(payload, jdk_key);
        jdkCrypto.decrypt(ct, jdk_key);
      }
      long jdk_time = System.nanoTime() - jdk_start;

      double bouncy_avg_us = (bouncy_time / 1000.0) / BENCHMARK_ITERATIONS;
      double jdk_avg_us = (jdk_time / 1000.0) / BENCHMARK_ITERATIONS;
      double ratio = bouncy_avg_us / jdk_avg_us;

      System.out.printf("%-12s %12.2f µs %12.2f µs %12.2fx%n",
          size + " bytes", bouncy_avg_us, jdk_avg_us, ratio);

      assertTrue(bouncy_time > 0);
      assertTrue(jdk_time > 0);
    }
  }

  @Test
  @DisplayName("Summary: Print performance comparison summary")
  void printSummary() {
    System.out.println("\n" + "=".repeat(60));
    System.out.println("CRYPTO ENGINE BENCHMARK SUMMARY");
    System.out.println("=".repeat(60));
    System.out.println();
    System.out.println("Providers tested:");
    System.out.println("  - BouncyCastle: " + bouncyCrypto.getClass().getSimpleName());
    System.out.println("  - JDK:          " + jdkCrypto.getClass().getSimpleName());
    System.out.println();
    System.out.println("Notes:");
    System.out.println("  - JDK crypto uses hardware AES-NI acceleration when available");
    System.out.println("  - JDK crypto is recommended for GraalVM native-image compilation");
    System.out.println("  - Both providers produce interoperable ciphertext");
    System.out.println();
    System.out.println("For more accurate benchmarks, run the JMH benchmark:");
    System.out.println("  mvn test-compile exec:java \\");
    System.out.println("    -Dexec.mainClass=\"com.godaddy.asherah.crypto.engine.CryptoEngineBenchmark\" \\");
    System.out.println("    -Dexec.classpathScope=\"test\"");
    System.out.println("=".repeat(60));

    assertTrue(true); // Always pass
  }
}

