package com.godaddy.asherah.crypto.engine;

import java.security.SecureRandom;
import java.util.concurrent.TimeUnit;

import org.openjdk.jmh.annotations.Benchmark;
import org.openjdk.jmh.annotations.BenchmarkMode;
import org.openjdk.jmh.annotations.Fork;
import org.openjdk.jmh.annotations.Level;
import org.openjdk.jmh.annotations.Measurement;
import org.openjdk.jmh.annotations.Mode;
import org.openjdk.jmh.annotations.OutputTimeUnit;
import org.openjdk.jmh.annotations.Param;
import org.openjdk.jmh.annotations.Scope;
import org.openjdk.jmh.annotations.Setup;
import org.openjdk.jmh.annotations.State;
import org.openjdk.jmh.annotations.Warmup;
import org.openjdk.jmh.infra.Blackhole;
import org.openjdk.jmh.results.format.ResultFormatType;
import org.openjdk.jmh.runner.Runner;
import org.openjdk.jmh.runner.RunnerException;
import org.openjdk.jmh.runner.options.Options;
import org.openjdk.jmh.runner.options.OptionsBuilder;

import com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAes256GcmCrypto;
import com.godaddy.asherah.crypto.engine.jdk.JdkAes256GcmCrypto;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;

/**
 * JMH Benchmark comparing BouncyCastle and JDK AES-256-GCM implementations.
 *
 * <p>Run from command line:
 * <pre>
 * mvn clean test-compile exec:java \
 *   -Dexec.mainClass="com.godaddy.asherah.crypto.engine.CryptoEngineBenchmark" \
 *   -Dexec.classpathScope="test"
 * </pre>
 *
 * <p>Or run the main method directly from your IDE.
 */
@BenchmarkMode(Mode.Throughput)
@OutputTimeUnit(TimeUnit.MILLISECONDS)
@State(Scope.Benchmark)
@Fork(value = 1, jvmArgs = {"-Xms2G", "-Xmx2G"})
@Warmup(iterations = 3, time = 1)
@Measurement(iterations = 5, time = 1)
public class CryptoEngineBenchmark {

  @Param({"64", "256", "1024", "4096", "16384"})
  private int payloadSize;

  private AeadEnvelopeCrypto bouncyCrypto;
  private AeadEnvelopeCrypto jdkCrypto;

  private CryptoKey bouncy_key;
  private CryptoKey jdk_key;

  private byte[] plaintext;
  private byte[] bouncy_ciphertext;
  private byte[] jdk_ciphertext;

  @Setup(Level.Trial)
  public void setup() {
    bouncyCrypto = new BouncyAes256GcmCrypto();
    jdkCrypto = new JdkAes256GcmCrypto();

    bouncy_key = bouncyCrypto.generateKey();
    jdk_key = jdkCrypto.generateKey();

    plaintext = new byte[payloadSize];
    new SecureRandom().nextBytes(plaintext);

    // Pre-encrypt for decrypt benchmarks
    bouncy_ciphertext = bouncyCrypto.encrypt(plaintext, bouncy_key);
    jdk_ciphertext = jdkCrypto.encrypt(plaintext, jdk_key);
  }

  // ==================== Encryption Benchmarks ====================

  @Benchmark
  public void bouncy_encrypt(Blackhole bh) {
    bh.consume(bouncyCrypto.encrypt(plaintext, bouncy_key));
  }

  @Benchmark
  public void jdk_encrypt(Blackhole bh) {
    bh.consume(jdkCrypto.encrypt(plaintext, jdk_key));
  }

  // ==================== Decryption Benchmarks ====================

  @Benchmark
  public void bouncy_decrypt(Blackhole bh) {
    bh.consume(bouncyCrypto.decrypt(bouncy_ciphertext, bouncy_key));
  }

  @Benchmark
  public void jdk_decrypt(Blackhole bh) {
    bh.consume(jdkCrypto.decrypt(jdk_ciphertext, jdk_key));
  }

  // ==================== Round Trip Benchmarks ====================

  @Benchmark
  public void bouncy_roundTrip(Blackhole bh) {
    byte[] encrypted = bouncyCrypto.encrypt(plaintext, bouncy_key);
    bh.consume(bouncyCrypto.decrypt(encrypted, bouncy_key));
  }

  @Benchmark
  public void jdk_roundTrip(Blackhole bh) {
    byte[] encrypted = jdkCrypto.encrypt(plaintext, jdk_key);
    bh.consume(jdkCrypto.decrypt(encrypted, jdk_key));
  }

  // ==================== Envelope Encryption Benchmarks ====================

  @Benchmark
  public void bouncy_envelopeEncrypt(Blackhole bh) {
    bh.consume(bouncyCrypto.envelopeEncrypt(plaintext, bouncy_key));
  }

  @Benchmark
  public void jdk_envelopeEncrypt(Blackhole bh) {
    bh.consume(jdkCrypto.envelopeEncrypt(plaintext, jdk_key));
  }

  /**
   * Main method to run the benchmark.
   * Can be executed from IDE or command line.
   */
  public static void main(String[] args) throws RunnerException {
    Options opt = new OptionsBuilder()
        .include(CryptoEngineBenchmark.class.getSimpleName())
        .resultFormat(ResultFormatType.TEXT)
        .build();

    new Runner(opt).run();
  }
}

