using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GoDaddy.Asherah.PlatformNative.LP64.Linux
{
  public static class OpenSSLCrypto
  {
    public const string LibraryName = "libcrypto.so.1.1";
    public const int EVP_MAX_BLOCK_LENGTH = 32;
    public const int EVP_MAX_KEY_LENGTH = 64;
    public const int EVP_MAX_IV_LENGTH = 16;

    [DllImport(LibraryName, EntryPoint = "EVP_CIPHER_CTX_new", SetLastError = true)]
    private static extern IntPtr _EVP_CIPHER_CTX_new();

    public static IntPtr EVP_CIPHER_CTX_new()
    {
      return _EVP_CIPHER_CTX_new();
    }

    [DllImport(LibraryName, EntryPoint = "EVP_CIPHER_CTX_free", SetLastError = true)]
    private static extern void _EVP_CIPHER_CTX_free(IntPtr ctx);

    public static void EVP_CIPHER_CTX_free(IntPtr ctx)
    {
      _EVP_CIPHER_CTX_free(ctx);
    }

    [DllImport(LibraryName, EntryPoint = "EVP_get_cipherbyname", SetLastError = true)]
    private static extern IntPtr _EVP_get_cipherbyname([MarshalAs(UnmanagedType.LPStr)] string name);

    public static IntPtr EVP_get_cipherbyname(string name)
    {
      return _EVP_get_cipherbyname(name);
    }

    [DllImport(LibraryName, EntryPoint = "EVP_EncryptInit_ex", SetLastError = true)]
    private static extern int _EVP_EncryptInit_ex(IntPtr ctx, IntPtr type, IntPtr impl, IntPtr key, IntPtr iv);

    public static int EVP_EncryptInit_ex(IntPtr ctx, IntPtr type, IntPtr impl, IntPtr key, IntPtr iv)
    {
      return _EVP_EncryptInit_ex(ctx, type, impl, key, iv);
    }

    [DllImport(LibraryName, EntryPoint = "EVP_EncryptUpdate", SetLastError = true)]
    private static extern int _EVP_EncryptUpdate(IntPtr ctx, IntPtr outPtr, [MarshalAs(UnmanagedType.LPArray)] byte[] outLength, IntPtr inptr, int inLength);

    public static int EVP_EncryptUpdate(IntPtr ctx, IntPtr outPtr, out int outLength, IntPtr inPtr, int inLength)
    {
      var outLengthBuf = new byte[4];

      var result = _EVP_EncryptUpdate(ctx, outPtr, outLengthBuf, inPtr, inLength);

      outLength = BitConverter.ToInt32(outLengthBuf, 0);

      return result;
    }

    [DllImport(LibraryName, EntryPoint = "EVP_EncryptFinal_ex", SetLastError = true)]
    private static extern int _EVP_EncryptFinal_ex(IntPtr ctx, IntPtr outPtr, [MarshalAs(UnmanagedType.LPArray)] byte[] outlength);

    public static int EVP_EncryptFinal_ex(IntPtr ctx, IntPtr outPtr, out int outLength)
    {
      var outLengthBuf = new byte[4];

      var result = _EVP_EncryptFinal_ex(ctx, outPtr, outLengthBuf);

      outLength = BitConverter.ToInt32(outLengthBuf, 0);

      return result;
    }

    [DllImport(LibraryName, EntryPoint = "EVP_DecryptInit_ex", SetLastError = true)]
    private static extern int _EVP_DecryptInit_ex(IntPtr ctx, IntPtr type, IntPtr impl, IntPtr key, IntPtr iv);

    public static int EVP_DecryptInit_ex(IntPtr ctx, IntPtr type, IntPtr impl, IntPtr key, IntPtr iv)
    {
      return _EVP_DecryptInit_ex(ctx, type, impl, key, iv);
    }

    [DllImport(LibraryName, EntryPoint = "EVP_DecryptUpdate", SetLastError = true)]
    private static extern int _EVP_DecryptUpdate(IntPtr ctx, IntPtr outptr, [MarshalAs(UnmanagedType.LPArray)] byte[] outlength, IntPtr inptr, int inlength);

    public static int EVP_DecryptUpdate(IntPtr ctx, IntPtr outptr, out int outlength, IntPtr inptr, int inlength)
    {
      var outLengthBuf = new byte[4];

      var result = _EVP_DecryptUpdate(ctx, outptr, outLengthBuf, inptr, inlength);

      outlength = BitConverter.ToInt32(outLengthBuf, 0);

      return result;
    }

    [DllImport(LibraryName, EntryPoint = "EVP_DecryptFinal_ex", SetLastError = true)]
    private static extern int _EVP_DecryptFinal_ex(IntPtr ctx, IntPtr outPtr, [MarshalAs(UnmanagedType.LPArray)] byte[] outlength);

    public static int EVP_DecryptFinal_ex(IntPtr ctx, IntPtr outPtr, out int outLength)
    {
      var outLengthBuf = new byte[4];

      var result = _EVP_DecryptFinal_ex(ctx, outPtr, outLengthBuf);

      outLength = BitConverter.ToInt32(outLengthBuf, 0);

      return result;
    }

    [DllImport(LibraryName, EntryPoint = "RAND_bytes", SetLastError = true)]
    private static extern int _RAND_bytes(IntPtr buf, int num);

    public static int RAND_bytes(IntPtr buf, int num)
    {
      return _RAND_bytes(buf, num);
    }

    [DllImport(LibraryName, EntryPoint = "EVP_CIPHER_block_size", SetLastError = true)]
    private static extern int _EVP_CIPHER_block_size(IntPtr e);

    public static int EVP_CIPHER_block_size(IntPtr e)
    {
      var blockSize = _EVP_CIPHER_block_size(e);

      // BUG: EVP_CIPHER_block_size returns 1
      if (blockSize == 1)
      {
        blockSize = EVP_MAX_BLOCK_LENGTH;
        Debug.WriteLine("BUG: Adjusted block size: " + blockSize);
      }

      return blockSize;
    }

    [DllImport(LibraryName, EntryPoint = "EVP_CIPHER_key_length", SetLastError = true)]
    private static extern int _EVP_CIPHER_key_length(IntPtr e);

    public static int EVP_CIPHER_key_length(IntPtr e)
    {
      return _EVP_CIPHER_key_length(e);
    }

    [DllImport(LibraryName, EntryPoint = "EVP_CIPHER_iv_length", SetLastError = true)]
    private static extern int _EVP_CIPHER_iv_length(IntPtr e);

    public static int EVP_CIPHER_iv_length(IntPtr e)
    {
      return _EVP_CIPHER_iv_length(e);
    }
  }
}
