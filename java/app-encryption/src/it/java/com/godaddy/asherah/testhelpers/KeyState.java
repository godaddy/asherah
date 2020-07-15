package com.godaddy.asherah.testhelpers;

import org.apache.commons.text.WordUtils;

public enum KeyState {
  RETIRED,
  VALID,
  EMPTY;

  @Override
  public String toString() {
    return WordUtils.capitalizeFully(name());
  }
}
