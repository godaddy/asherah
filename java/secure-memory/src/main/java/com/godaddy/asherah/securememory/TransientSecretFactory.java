package com.godaddy.asherah.securememory;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.securememory.protectedmemoryimpl.ProtectedMemorySecretFactory;

/**
 * Factory for creating transient (protected memory) secrets.
 *
 * <p>Automatically selects the best available implementation:
 * <ul>
 *   <li>FFM (Foreign Function & Memory API) on Java 22+ - better performance</li>
 *   <li>JNA fallback on older JVMs</li>
 * </ul>
 *
 * <p>Use {@link #setPreferJna(boolean)} to force JNA even on Java 22+.
 */
public class TransientSecretFactory implements SecretFactory {
  private static final Logger LOG = LoggerFactory.getLogger(TransientSecretFactory.class);

  /** Minimum Java version required for FFM support. */
  private static final int FFM_MIN_JAVA_VERSION = 22;

  private static volatile boolean preferJna = false;
  private final SecretFactory secretFactory;

  /**
   * Creates a new TransientSecretFactory.
   * Automatically selects FFM on Java 22+ or JNA as fallback.
   */
  public TransientSecretFactory() {
    secretFactory = createBestAvailableFactory();
  }

  /**
   * Sets whether to prefer JNA over FFM even when FFM is available.
   * This is useful for testing or if FFM causes issues.
   *
   * @param prefer true to prefer JNA, false to auto-detect (default)
   */
  public static void setPreferJna(final boolean prefer) {
    preferJna = prefer;
  }

  /**
   * Checks if FFM implementation is being used.
   *
   * @return true if using FFM, false if using JNA
   */
  public boolean isUsingFfm() {
    return secretFactory.getClass().getName().contains("Ffm");
  }

  private SecretFactory createBestAvailableFactory() {
    if (!preferJna && isFfmAvailable()) {
      try {
        SecretFactory ffmFactory = createFfmFactory();
        if (ffmFactory != null) {
          LOG.info("Using FFM-based SecretFactory (Java 22+ detected)");
          return ffmFactory;
        }
      }
      catch (Exception e) {
        LOG.warn("FFM SecretFactory initialization failed, falling back to JNA: {}", e.getMessage());
      }
    }

    LOG.info("Using JNA-based SecretFactory");
    return new ProtectedMemorySecretFactory();
  }

  /**
   * Checks if FFM is available on the current JVM.
   */
  private boolean isFfmAvailable() {
    try {
      int version = Runtime.version().feature();
      if (version < FFM_MIN_JAVA_VERSION) {
        return false;
      }

      // Check if FFM classes are available
      Class.forName("java.lang.foreign.MemorySegment");
      Class.forName("java.lang.foreign.Linker");
      return true;
    }
    catch (ClassNotFoundException e) {
      return false;
    }
  }

  /**
   * Creates the FFM factory using reflection to avoid compile-time dependency.
   * This allows the code to compile on Java 17+ even though FFM requires Java 22+.
   */
  private SecretFactory createFfmFactory() {
    try {
      Class<?> ffmFactoryClass = Class.forName(
          "com.godaddy.asherah.securememory.ffmimpl.FfmSecretFactory");
      return (SecretFactory) ffmFactoryClass.getDeclaredConstructor().newInstance();
    }
    catch (Exception e) {
      LOG.debug("Could not create FFM factory: {}", e.getMessage());
      return null;
    }
  }

  @Override
  public Secret createSecret(final byte[] secretData) {
    return secretFactory.createSecret(secretData);
  }

  @Override
  public Secret createSecret(final char[] secretData) {
    return secretFactory.createSecret(secretData);
  }
}
