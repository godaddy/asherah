package com.godaddy.asherah.securememory.ffmimpl;

import java.lang.foreign.FunctionDescriptor;
import java.lang.foreign.Linker;
import java.lang.foreign.SymbolLookup;
import java.lang.invoke.MethodHandle;

/**
 * Shared FFM linker / symbol-lookup utility for the libc downcalls used by the FFM allocators.
 *
 * <p>All resolution happens at first use, not class init, so this type is safe to reference from
 * code that may be initialized at GraalVM native-image build time. The {@code Linker} and
 * {@code SymbolLookup} live inside a private holder class that is only loaded the first time
 * a downcall is requested.
 *
 * <p>This class is intentionally final and stateless; consumers should keep their own
 * {@code static final MethodHandle} fields inside their own private holder classes
 * (see {@link FfmProtectedMemoryAllocator} for an example) so that each allocator class only
 * pays the resolution cost once and only when actually used.
 */
final class NativeLibc {

  private NativeLibc() {
  }

  /**
   * Resolves a libc symbol and binds it as a downcall {@link MethodHandle} with the given
   * function signature.
   *
   * @param symbol     the libc symbol name (e.g. {@code "mmap"})
   * @param descriptor the function descriptor describing the native signature
   * @return a {@link MethodHandle} that invokes the native function via FFM
   * @throws FfmOperationFailed if the symbol cannot be located in libc
   */
  static MethodHandle downcall(final String symbol, final FunctionDescriptor descriptor) {
    return Holder.LINKER.downcallHandle(
        Holder.LIBC.find(symbol)
            .orElseThrow(() -> new FfmOperationFailed("libc symbol not found: " + symbol)),
        descriptor);
  }

  /**
   * Initialization-on-Demand Holder. JLS guarantees thread-safe lazy class init without
   * volatile or synchronized.
   */
  private static final class Holder {
    static final Linker LINKER = Linker.nativeLinker();
    static final SymbolLookup LIBC = LINKER.defaultLookup();

    private Holder() {
    }
  }
}
