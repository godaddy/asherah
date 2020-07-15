package com.godaddy.asherah.regression;

import com.godaddy.asherah.testhelpers.KeyState;

class DecryptMetastoreInteractions {

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

