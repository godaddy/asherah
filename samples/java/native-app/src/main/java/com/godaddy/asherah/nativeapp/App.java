package com.godaddy.asherah.nativeapp;

import java.nio.charset.StandardCharsets;
import java.util.Base64;

import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.appencryption.Session;
import com.godaddy.asherah.appencryption.SessionFactory;
import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.appencryption.persistence.InMemoryMetastoreImpl;
import com.godaddy.asherah.appencryption.persistence.Metastore;
import com.godaddy.asherah.crypto.BasicExpiringCryptoPolicy;
import com.godaddy.asherah.crypto.CryptoPolicy;
import com.godaddy.asherah.crypto.engine.CryptoEngineType;

/**
 * GraalVM Native Image sample application demonstrating Asherah encryption
 * using the JDK crypto provider (no BouncyCastle).
 *
 * <p>This sample uses:
 * <ul>
 *   <li>JDK AES-256-GCM crypto engine (hardware-accelerated on modern CPUs)</li>
 *   <li>FFM-based secure memory (Java 22+) for key protection</li>
 *   <li>In-memory metastore (for demonstration)</li>
 *   <li>Static key management service (for demonstration)</li>
 * </ul>
 *
 * <p>Build native image: {@code mvn -Pnative package}
 * <p>Run native: {@code ./target/asherah-native}
 */
public final class App {

  private static final Logger LOG = LoggerFactory.getLogger(App.class);

  private static final int KEY_EXPIRATION_DAYS = 30;
  private static final int CACHE_CHECK_MINUTES = 30;
  private static final int DEFAULT_ITERATIONS = 3;

  private App() {
    // Utility class
  }

  /**
   * Main entry point.
   *
   * @param args command line arguments: [iterations] [payload]
   */
  public static void main(final String[] args) {
    int iterations = DEFAULT_ITERATIONS;
    String payload = "Hello from GraalVM Native Image!";

    // Parse optional arguments
    if (args.length > 0) {
      try {
        iterations = Integer.parseInt(args[0]);
      } catch (NumberFormatException e) {
        LOG.warn("Invalid iterations argument, using default: {}", DEFAULT_ITERATIONS);
      }
    }
    if (args.length > 1) {
      payload = args[1];
    }

    LOG.info("=== Asherah GraalVM Native Image Sample ===");
    LOG.info("Crypto Engine: JDK AES-256-GCM");
    LOG.info("Java Version: {}", System.getProperty("java.version"));
    LOG.info("Iterations: {}", iterations);
    LOG.info("Payload: {}", payload);
    LOG.info("");

    try {
      runEncryptionDemo(iterations, payload);
    } catch (Exception e) {
      LOG.error("Encryption demo failed", e);
      System.exit(1);
    }
  }

  private static void runEncryptionDemo(final int iterations, final String originalPayload) {
    // Setup in-memory metastore (for demo purposes)
    Metastore<JSONObject> metastore = new InMemoryMetastoreImpl<>();
    LOG.info("Using in-memory metastore");

    // Setup static key management service with FFM-based secure memory (for demo purposes)
    // Uses NativeStaticKeyManagementService which directly uses FFM for native-image compatibility
    KeyManagementService keyManagementService =
        new NativeStaticKeyManagementService("thisIsAStaticMasterKeyForTesting");
    LOG.info("Using static key management service with FFM secure memory");

    // Configure crypto policy
    CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy
        .newBuilder()
        .withKeyExpirationDays(KEY_EXPIRATION_DAYS)
        .withRevokeCheckMinutes(CACHE_CHECK_MINUTES)
        .build();

    // Create session factory with JDK crypto engine (no BouncyCastle)
    LOG.info("Creating session factory with JDK crypto engine...");
    try (SessionFactory sessionFactory = SessionFactory
        .newBuilder("productId", "native_app")
        .withMetastore(metastore)
        .withCryptoPolicy(cryptoPolicy)
        .withKeyManagementService(keyManagementService)
        .withCryptoEngine(CryptoEngineType.JDK)  // Explicitly use JDK crypto
        .build()) {

      // Create session for a partition
      try (Session<byte[], byte[]> session = sessionFactory.getSessionBytes("user123")) {

        long totalEncryptTime = 0;
        long totalDecryptTime = 0;

        for (int i = 1; i <= iterations; i++) {
          LOG.info("--- Iteration {} ---", i);

          // Encrypt
          long encryptStart = System.nanoTime();
          byte[] encrypted = session.encrypt(originalPayload.getBytes(StandardCharsets.UTF_8));
          long encryptTime = System.nanoTime() - encryptStart;
          totalEncryptTime += encryptTime;

          String encryptedBase64 = Base64.getEncoder().encodeToString(encrypted);
          LOG.info("Encrypted (Base64): {}...", encryptedBase64.substring(0, Math.min(50, encryptedBase64.length())));
          LOG.info("Encrypt time: {} µs", encryptTime / 1000);

          // Decrypt
          long decryptStart = System.nanoTime();
          byte[] decrypted = session.decrypt(encrypted);
          long decryptTime = System.nanoTime() - decryptStart;
          totalDecryptTime += decryptTime;

          String decryptedPayload = new String(decrypted, StandardCharsets.UTF_8);
          boolean matches = originalPayload.equals(decryptedPayload);

          LOG.info("Decrypted: {}", decryptedPayload);
          LOG.info("Decrypt time: {} µs", decryptTime / 1000);
          LOG.info("Matches original: {}", matches);

          if (!matches) {
            throw new IllegalStateException("Decrypted payload does not match original!");
          }
        }

        // Summary
        LOG.info("");
        LOG.info("=== Summary ===");
        LOG.info("Total iterations: {}", iterations);
        LOG.info("Average encrypt time: {} µs", (totalEncryptTime / iterations) / 1000);
        LOG.info("Average decrypt time: {} µs", (totalDecryptTime / iterations) / 1000);
        LOG.info("All iterations successful!");
      }
    }
  }
}

