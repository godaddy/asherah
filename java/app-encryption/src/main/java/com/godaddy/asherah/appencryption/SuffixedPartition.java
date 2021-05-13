package com.godaddy.asherah.appencryption;

public class SuffixedPartition extends Partition {

  private final String regionSuffix;

  /**
   * Create a new {@code SuffixedPartition} instance using the provided parameters.
   *
   * @param partitionId A unique identifier for a {@link Session}.
   * @param serviceId A unique identifier for a service, used to create a {@link SessionFactory} object.
   * @param productId A unique identifier for a product, used to create a {@link SessionFactory} object.
   * @param regionSuffix A key suffix that prevents multi-region writes from clobbering each other and
   * ensures that no keys are lost.
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

  @Override
  public boolean isValidIntermediateKeyId(final String id) {
    return id.equals(getIntermediateKeyId()) || id.startsWith(super.getIntermediateKeyId());
  }
}
