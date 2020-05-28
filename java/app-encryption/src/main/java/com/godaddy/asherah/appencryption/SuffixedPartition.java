package com.godaddy.asherah.appencryption;

public class SuffixedPartition extends Partition {

  private final String regionSuffix;

  public SuffixedPartition(final String partitionId, final String serviceId, final String productId,
                           final String regionSuffix) {
    super(partitionId, serviceId, productId);
    this.regionSuffix = regionSuffix;
  }

  @Override
  public String getSystemKeyId() {
    return "_SK_" + getServiceId() + "_" + getProductId() + "_" + this.regionSuffix;
  }

  @Override
  public String getIntermediateKeyId() {
    return "_IK_" + getPartitionId() + "_" + getServiceId() + "_" + getProductId() + "_" + this.regionSuffix;
  }

  @Override
  public String toString() {
    return getClass().getSimpleName() + "[partitionId=" + getPartitionId() +
      ", serviceId=" + getServiceId() + ", productId=" + getProductId() + ", regionSuffix=" + this.regionSuffix + "]";
  }
}
