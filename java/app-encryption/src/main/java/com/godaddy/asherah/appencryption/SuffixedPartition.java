package com.godaddy.asherah.appencryption;

public class SuffixedPartition extends Partition {

  private final String regionSuffix;

  /**
   * Constructor for {@link SuffixedPartition}.
   * @param partitionId Identifier for the partition.
   * @param serviceId Service Id for the partition.
   * @param productId Product Id for the partition.
   * @param regionSuffix Region suffix for the partition.
   */
  public SuffixedPartition(final String partitionId, final String serviceId, final String productId,
                           final String regionSuffix) {
    super(partitionId, serviceId, productId);
    this.regionSuffix = regionSuffix;
  }

  @Override
  public String getSystemKeyId() {
    return super.getSystemKeyId() + "_" + this.regionSuffix;
  }

  @Override
  public String getIntermediateKeyId() {
    return super.getIntermediateKeyId() + "_" + this.regionSuffix;
  }

  @Override
  public String toString() {
    return getClass().getSimpleName() + "[partitionId=" + getPartitionId() +
      ", serviceId=" + getServiceId() + ", productId=" + getProductId() + ", regionSuffix=" + this.regionSuffix + "]";
  }
}
