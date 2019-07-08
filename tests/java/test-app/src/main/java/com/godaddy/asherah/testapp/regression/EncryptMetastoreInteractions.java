package com.godaddy.asherah.testapp.regression;

import com.godaddy.asherah.testapp.testhelpers.KeyState;

class EncryptMetastoreInteractions {

  private KeyState cacheIK;
  private KeyState metaIK;
  private KeyState cacheSK;
  private KeyState metaSK;

  protected EncryptMetastoreInteractions(final KeyState cacheIK, final KeyState metaIK,
                                         final KeyState cacheSK, final KeyState metaSK) {

    this.cacheIK = cacheIK;
    this.metaIK = metaIK;
    this.cacheSK = cacheSK;
    this.metaSK = metaSK;
  }

  protected boolean shouldStoreIK() {

    if (cacheIK == KeyState.VALID) {
      return false;
    }

    if (metaIK != KeyState.VALID) {
      return true;
    }

    // At this stage IK is valid in metastore.
    // The existing IK can only be used if the SK is valid in cache,
    if (cacheSK == KeyState.VALID) {
      return false;
    }
    // or if the SK is missing from the cache but is valid in metastore.
    else if (cacheSK == KeyState.EMPTY) {
      if (metaSK == KeyState.VALID) {
        return false;
      }
    }

    return true;
  }

  protected boolean shouldStoreSK() {

    if (cacheIK == KeyState.VALID) {
      return false;
    }

    return cacheSK != KeyState.VALID && metaSK != KeyState.VALID;
  }

  protected boolean shouldLoadSK() {

    if (cacheIK == KeyState.VALID) {
      return false;
    }

    if (metaIK != KeyState.VALID) {
      return false;
    }

    if (cacheSK == KeyState.EMPTY) {
      return true;
    }

    return false;
  }

  protected boolean shouldLoadLatestIK() {
    return cacheIK != KeyState.VALID;
  }

  protected boolean shouldLoadLatestSK() {

    if (cacheIK == KeyState.VALID) {
      return false;
    }

    if (metaIK == KeyState.VALID) {
      return false;
    }

    return cacheSK != KeyState.VALID;
  }
}
