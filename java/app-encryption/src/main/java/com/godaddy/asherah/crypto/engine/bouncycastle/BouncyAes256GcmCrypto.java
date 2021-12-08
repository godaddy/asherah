package com.godaddy.asherah.crypto.engine.bouncycastle;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;

import org.bouncycastle.crypto.engines.AESEngine;
import org.bouncycastle.crypto.io.CipherInputStream;
import org.bouncycastle.crypto.io.CipherOutputStream;
import org.bouncycastle.crypto.modes.AEADBlockCipher;
import org.bouncycastle.crypto.modes.AEADCipher;
import org.bouncycastle.crypto.modes.GCMBlockCipher;
import org.bouncycastle.crypto.params.AEADParameters;

import com.amazonaws.util.IOUtils;
import com.godaddy.asherah.crypto.keys.CryptoKey;

public class BouncyAes256GcmCrypto extends BouncyAeadCrypto {

  @Override
  public void encryptStream(InputStream inputStream, OutputStream outputStream,
                                    CryptoKey key) {
    byte[] nonce = generateNonce();
    AEADCipher cipher = getNewAeadCipherInstance();

    AEADParameters cipherParameters = getParameters(key, nonce);
    try {
      cipher.init(true, cipherParameters);
      outputStream.write(nonce);
      CipherOutputStream cipherOut = new CipherOutputStream(outputStream, (AEADBlockCipher) cipher);
      IOUtils.copy(inputStream, cipherOut);
      cipherOut.close();
    } catch (IOException e) {
      e.printStackTrace();
    }
  }

  @Override
  public void decryptStream(InputStream inputStream, OutputStream outputStream,
                            CryptoKey key) {
    byte[] nonce = generateNonce();
    AEADCipher cipher = getNewAeadCipherInstance();

    AEADParameters cipherParameters = getParameters(key, nonce);
    cipher.init(false, cipherParameters);

    CipherInputStream cipherIn = new CipherInputStream(inputStream, (AEADBlockCipher) cipher);
    try {
      IOUtils.copy(cipherIn, outputStream);
      cipherIn.close();
    } catch (IOException e) {
      e.printStackTrace();
    }
  }

  /**
   * Creates a new {@link com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAeadCrypto} instance.
   */
  public BouncyAes256GcmCrypto() {
    super();
  }

  @Override
  protected AEADCipher getNewAeadCipherInstance() {
    return new GCMBlockCipher(new AESEngine());
  }
}
