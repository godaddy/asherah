package com.godaddy.asherah.securememory;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.securememory.protectedmemoryimpl.ProtectedMemorySecretFactory;

/**
 * Factory for creating transient (protected memory) secrets.
 *
 * <p>Automatically selects the best available implementation:
 * <ul>
 *   <li>FFM (Foreign Function &amp; Memory API) on Java 22+ — better performance, GraalVM
 *       native-image compatible.</li>
 *   <li>JNA fallback when FFM is unavailable.</li>
 * </ul>
 *
 * <p>The FFM implementation lives in the multi-release {@code META-INF/versions/22} section
 * of the JAR and is loaded reflectively, so this class compiles and runs on Java 17 even
 * though FFM itself requires 22+.
 *
 * <p>FFM preference can be overridden globally via {@link #setPreferJna(boolean)} (e.g. for
 * testing or to force the JNA path on a JVM where FFM has known issues).
 */
public class TransientSecretFactory implements SecretFactory {
  private static final Logger LOG = LoggerFactory.getLogger(TransientSecretFactory.class);

  /** Minimum Java feature version required for FFM support. */
  private static final int FFM_MIN_JAVA_VERSION = 22;

  /** Fully-qualified name of the FFM factory; loaded reflectively to keep Java 17 compat. */
  private static final String FFM_FACTORY_CLASS =
      "com.godaddy.asherah.securememory.ffmimpl.FfmSecretFactory";

  private static volatile boolean preferJna = false;

  private final SecretFactory delegate;
  private final boolean usingFfm;

  /**
   * Creates a TransientSecretFactory using the global {@link #setPreferJna(boolean)} setting.
   */
  public TransientSecretFactory() {
    this(preferJna);
  }

  /**
   * Creates a TransientSecretFactory with an explicit JNA preference.
   *
   * @param forceJna if true, skip FFM detection entirely and use JNA
   */
  public TransientSecretFactory(final boolean forceJna) {
    SecretFactory ffm = null;
    if (!forceJna) {
      ffm = tryCreateFfmFactory();
    }
    if (ffm != null) {
      this.delegate = ffm;
      this.usingFfm = true;
      LOG.info("Using FFM-based SecretFactory");
    }
    else {
      this.delegate = new ProtectedMemorySecretFactory();
      this.usingFfm = false;
      LOG.info("Using JNA-based SecretFactory");
    }
  }

  /**
   * Sets whether new {@link TransientSecretFactory} instances should prefer JNA over FFM
   * even when FFM is available. Useful for testing or working around FFM-specific issues.
   *
   * @param prefer true to prefer JNA, false to auto-detect (default)
   */
  public static void setPreferJna(final boolean prefer) {
    preferJna = prefer;
  }

  /**
   * Returns true if this factory is using the FFM implementation, false if it is using JNA.
   */
  public boolean isUsingFfm() {
    return usingFfm;
  }

  /**
   * Attempts to construct an FFM-based factory. Returns {@code null} (no exception) if FFM is
   * not available on this JVM, the platform is unsupported, or any other initialization
   * failure occurs — callers fall back to JNA.
   */
  private static SecretFactory tryCreateFfmFactory() {
    if (Runtime.version().feature() < FFM_MIN_JAVA_VERSION) {
      return null;
    }
    try {
      Class<?> ffmFactoryClass = Class.forName(FFM_FACTORY_CLASS);
      return (SecretFactory) ffmFactoryClass.getDeclaredConstructor().newInstance();
    }
    catch (ClassNotFoundException e) {
      // FFM classes not packaged (e.g. running outside the multi-release JAR).
      LOG.debug("FFM factory class not found: {}", e.getMessage());
      return null;
    }
    catch (ReflectiveOperationException e) {
      // Construction-time issue (e.g. unsupported platform, allocator init failure).
      LOG.warn("FFM SecretFactory initialization failed, falling back to JNA: {}",
          unwrapMessage(e));
      return null;
    }
  }

  private static String unwrapMessage(final Throwable t) {
    Throwable cause = t.getCause();
    if (cause == null) {
      cause = t;
    }
    return cause.getMessage();
  }

  @Override
  public Secret createSecret(final byte[] secretData) {
    return delegate.createSecret(secretData);
  }

  @Override
  public Secret createSecret(final char[] secretData) {
    return delegate.createSecret(secretData);
  }
}
