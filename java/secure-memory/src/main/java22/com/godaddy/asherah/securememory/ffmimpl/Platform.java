package com.godaddy.asherah.securememory.ffmimpl;

import java.util.Locale;

/**
 * Supported operating systems for the FFM allocator.
 *
 * <p>Encapsulates the {@code os.name} system-property heuristic so the rest of the code can
 * branch on a typed enum instead of duplicating string matching.
 */
enum Platform {
  LINUX,
  MACOS,
  UNSUPPORTED;

  /** Detects the current platform from the {@code os.name} system property. */
  static Platform current() {
    return fromOsName(System.getProperty("os.name", ""));
  }

  /** Visible for testing. */
  static Platform fromOsName(final String osName) {
    String normalized = osName.toLowerCase(Locale.ROOT);
    if (normalized.contains("mac") || normalized.contains("darwin")) {
      return MACOS;
    }
    if (normalized.contains("linux")) {
      return LINUX;
    }
    return UNSUPPORTED;
  }
}
