package com.godaddy.asherah.securememory.ffmimpl;

import com.godaddy.asherah.securememory.Secret;
import com.godaddy.asherah.securememory.SecretFactory;

/**
 * Factory for creating FFM-based protected memory secrets.
 *
 * <p>Auto-detects the running platform (Linux / macOS) and instantiates the appropriate
 * {@link FfmAllocator}. Requires Java 22+ at runtime; this whole class lives in the
 * {@code java22} multi-release source root, so it cannot be loaded on earlier JVMs.
 */
public class FfmSecretFactory implements SecretFactory {

  /** Minimum Java feature version required for FFM support. */
  private static final int FFM_MIN_JAVA_VERSION = 22;

  private final FfmAllocator allocator;

  /**
   * Creates a new FFM secret factory.
   *
   * @throws UnsupportedOperationException if the platform is not supported
   */
  public FfmSecretFactory() {
    this(allocatorFor(Platform.current()));
  }

  /** Visible for testing — allows injecting an allocator. */
  FfmSecretFactory(final FfmAllocator allocator) {
    if (allocator == null) {
      throw new UnsupportedOperationException(
          "Could not detect supported platform for FFM protected memory");
    }
    this.allocator = allocator;
  }

  private static FfmAllocator allocatorFor(final Platform platform) {
    return switch (platform) {
      case LINUX -> new LinuxFfmProtectedMemoryAllocator();
      case MACOS -> new MacOSFfmProtectedMemoryAllocator();
      case UNSUPPORTED -> null;
    };
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
   * Returns true if FFM is available on the current JVM.
   *
   * <p>Because this class itself imports {@code java.lang.foreign.*}, it can only be loaded
   * on a JVM that exposes those types. Calling this method confirms both that the class
   * loaded successfully <em>and</em> that the runtime feature version meets the minimum.
   *
   * @return true if FFM is available (Java 22+), false otherwise
   */
  public static boolean isAvailable() {
    return Runtime.version().feature() >= FFM_MIN_JAVA_VERSION;
  }
}
