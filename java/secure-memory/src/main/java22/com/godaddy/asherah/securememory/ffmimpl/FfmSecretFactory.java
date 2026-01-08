package com.godaddy.asherah.securememory.ffmimpl;

import com.godaddy.asherah.securememory.Secret;
import com.godaddy.asherah.securememory.SecretFactory;

/**
 * Factory for creating FFM-based protected memory secrets.
 * Automatically detects the platform (Linux/macOS) and uses the appropriate allocator.
 * Requires Java 22+.
 */
public class FfmSecretFactory implements SecretFactory {

  /** Minimum Java version required for FFM support. */
  private static final int FFM_MIN_JAVA_VERSION = 22;

  private final FfmAllocator allocator;

  /**
   * Creates a new FFM secret factory.
   * Automatically detects the platform and creates the appropriate allocator.
   *
   * @throws UnsupportedOperationException if the platform is not supported
   */
  public FfmSecretFactory() {
    allocator = detectPlatformAllocator();
    if (allocator == null) {
      throw new UnsupportedOperationException("Could not detect supported platform for FFM protected memory");
    }
  }

  private FfmAllocator detectPlatformAllocator() {
    String osName = System.getProperty("os.name", "").toLowerCase();

    if (osName.contains("mac") || osName.contains("darwin")) {
      return new MacOSFfmProtectedMemoryAllocator();
    }
    else if (osName.contains("linux")) {
      return new LinuxFfmProtectedMemoryAllocator();
    }

    return null;
  }

  @Override
  public Secret createSecret(final byte[] secretData) {
    return new FfmProtectedMemorySecret(secretData, allocator);
  }

  @Override
  public Secret createSecret(final char[] secretData) {
    return FfmProtectedMemorySecret.fromCharArray(secretData, allocator);
  }

  /**
   * Checks if FFM is available on the current JVM.
   *
   * @return true if FFM is available (Java 22+), false otherwise
   */
  public static boolean isAvailable() {
    try {
      // Check if FFM classes are available (Java 22+)
      Class.forName("java.lang.foreign.MemorySegment");
      Class.forName("java.lang.foreign.Linker");

      // Also verify the runtime version
      int version = Runtime.version().feature();
      return version >= FFM_MIN_JAVA_VERSION;
    }
    catch (ClassNotFoundException e) {
      return false;
    }
  }
}

