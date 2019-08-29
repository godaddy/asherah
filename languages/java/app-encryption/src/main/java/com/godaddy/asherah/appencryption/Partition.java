package com.godaddy.asherah.appencryption;

public class Partition {

  private final String partitionId;
  private final String systemId;
  private final String productId;

  public Partition(final String partitionId, final String systemId, final String productId) {
    this.partitionId = partitionId;
    this.systemId = systemId;
    this.productId = productId;
  }

  String getPartitionId() {
    return partitionId;
  }

  String getSystemId() {
    return systemId;
  }

  String getProductId() {
    return productId;
  }

  public String getSystemKeyId() {
    return "_SK_" + systemId + "_" + productId;
  }

  public String getIntermediateKeyId() {
    return "_IK_" + partitionId + "_" + systemId + "_" + productId;
  }

  @Override
  public String toString() {
    return getClass().getSimpleName() + "[partitionId=" + partitionId +
      ", systemId=" + systemId + ", productId=" + productId + "]";
  }
}
