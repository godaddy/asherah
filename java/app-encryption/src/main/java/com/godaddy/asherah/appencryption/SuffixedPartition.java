package com.godaddy.asherah.appencryption;

public class SuffixedPartition extends Partition {

  private final String regionSuffix;

  /**
   * Creates a new {@code SuffixedPartition} instance using the provided parameters. An implementation of
   * {@link Partition} that
   * is used to support Global Tables in {@link com.godaddy.asherah.appencryption.persistence.DynamoDbMetastoreImpl}.
   *
   * @param partitionId A unique identifier for a {@link Session}.
   * @param serviceId A unique identifier for a service, used to create a {@link SessionFactory} object.
   * @param productId A unique identifier for a product, used to create a {@link SessionFactory} object.
   * @param regionSuffix The suffix to be added to a lookup key when using DynamoDB Global Tables.
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
