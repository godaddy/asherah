using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using size_t = System.UInt64;

namespace GoDaddy.Asherah.PlatformNative.LP64.Linux
{
  public static class LinuxOpenSSL11LP64
  {
    private const string LibraryName = "libcrypto.so.1.1";

    public static bool IsAvailable()
    {
      try
      {
        CRYPTO_secure_malloc_initialized();
      }
      catch (DllNotFoundException)
      {
        return false;
      }

      return true;
    }

    [DllImport(LibraryName, EntryPoint = "CRYPTO_secure_malloc_init", SetLastError = true)]
    private static extern int _CRYPTO_secure_malloc_init(size_t size, int minsize);

    public static int CRYPTO_secure_malloc_init(size_t size, int minsize)
    {
      // Round values up to nearest power of 2 as required by CRYPTO_secure_malloc_init
      size = (size_t)Math.Pow(2, (size_t)Math.Log(size - 1, 2) + 1);
      minsize = (int)Math.Pow(2, (int)Math.Log(minsize - 1, 2) + 1);

      if (CRYPTO_secure_malloc_initialized() == 1)
      {
        return 1;
      }

      // CRYPTO_secure_malloc_init() returns 0 on failure, 1 if successful, and 2 if successful but the heap could not be protected by memory mapping.
      return _CRYPTO_secure_malloc_init(size, minsize);
    }

    [DllImport(LibraryName, EntryPoint = "CRYPTO_secure_malloc_initialized", SetLastError = true)]
    private static extern int _CRYPTO_secure_malloc_initialized();

    public static int CRYPTO_secure_malloc_initialized()
    {
      // CRYPTO_secure_malloc_initialized() returns 1 if the secure heap is available (that is, if CRYPTO_secure_malloc_init() has been called, but CRYPTO_secure_malloc_done() has not been called or failed) or 0 if not.
      return _CRYPTO_secure_malloc_initialized();
    }

    [DllImport(LibraryName, EntryPoint = "CRYPTO_secure_malloc_done", SetLastError = true)]
    private static extern int _CRYPTO_secure_malloc_done();

    [ExcludeFromCodeCoverage]
    public static int CRYPTO_secure_malloc_done()
    {
      // CRYPTO_secure_malloc_done() returns 1 if the secure memory area is released, or 0 if not.
      return _CRYPTO_secure_malloc_done();
    }

    [DllImport(LibraryName, EntryPoint = "CRYPTO_secure_malloc", SetLastError = true)]
    private static extern IntPtr _CRYPTO_secure_malloc(size_t num, [MarshalAs(UnmanagedType.LPStr)] string file, int line);

    public static IntPtr CRYPTO_secure_malloc(size_t num, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
      // OPENSSL_secure_malloc() and OPENSSL_secure_zalloc() return a pointer into the secure heap of the requested size, or NULL if memory could not be allocated.
      return _CRYPTO_secure_malloc(num, file, line);
    }

    [DllImport(LibraryName, EntryPoint = "CRYPTO_secure_zalloc", SetLastError = true)]
    private static extern IntPtr _CRYPTO_secure_zalloc(size_t num, [MarshalAs(UnmanagedType.LPStr)] string file, int line);

    [ExcludeFromCodeCoverage]
    public static IntPtr CRYPTO_secure_zalloc(size_t num, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
      // OPENSSL_secure_malloc() and OPENSSL_secure_zalloc() return a pointer into the secure heap of the requested size, or NULL if memory could not be allocated.
      return _CRYPTO_secure_zalloc(num, file, line);
    }

    [DllImport(LibraryName, EntryPoint = "CRYPTO_secure_free", SetLastError = true)]
    private static extern void _CRYPTO_secure_free(IntPtr ptr, [MarshalAs(UnmanagedType.LPStr)] string file, int line);

    public static void CRYPTO_secure_free(IntPtr ptr, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
      // OPENSSL_secure_free() releases the memory at ptr back to the heap.
      _CRYPTO_secure_free(ptr, file, line);
    }

    [DllImport(LibraryName, EntryPoint = "CRYPTO_secure_clear_free", SetLastError = true)]
    private static extern void _CRYPTO_secure_clear_free(IntPtr ptr, size_t num, [MarshalAs(UnmanagedType.LPStr)] string file, int line);

    public static void CRYPTO_secure_clear_free(IntPtr ptr, size_t num, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
      // OPENSSL_secure_free() releases the memory at ptr back to the heap.
      _CRYPTO_secure_clear_free(ptr, num, file, line);
    }

    [DllImport(LibraryName, EntryPoint = "CRYPTO_secure_used", SetLastError = true)]
    private static extern size_t _CRYPTO_secure_used();

    [ExcludeFromCodeCoverage]
    public static size_t CRYPTO_secure_used()
    {
      // CRYPTO_secure_used() returns the number of bytes allocated in the secure heap.
      return _CRYPTO_secure_used();
    }
  }
}
