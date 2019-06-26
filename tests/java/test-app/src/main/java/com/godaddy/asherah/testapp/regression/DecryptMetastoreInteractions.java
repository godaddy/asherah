package com.godaddy.asherah.testapp.regression;

import com.godaddy.asherah.testapp.testhelpers.KeyState;

public class DecryptMetastoreInteractions {

  private KeyState cacheIK;
  private KeyState cacheSK;

  public DecryptMetastoreInteractions(final KeyState cacheIK, final KeyState cacheSK) {
    this.cacheIK = cacheIK;
    this.cacheSK = cacheSK;
  }

  protected boolean shouldLoadIK() {
    return cacheIK == KeyState.EMPTY;
  }

  protected boolean shouldLoadSK() {
    if (shouldLoadIK()) {
      return cacheSK == KeyState.EMPTY;
    }
    return false;
  }
}
