package com.godaddy.asherah.appencryption;

public class DefaultPartition extends Partition {

  /**
   * Constructor for {@link DefaultPartition}.
   * @param partitionId Identifier for the partition.
   * @param serviceId Service Id for the partition.
   * @param productId Product Id for the partition.
   */
  public DefaultPartition(final String partitionId, final String serviceId, final String productId) {
    super(partitionId, serviceId, productId);
  }

  @Override
  public String toString() {
    return getClass().getSimpleName() + "[partitionId=" + getPartitionId() +
      ", serviceId=" + getServiceId() + ", productId=" + getProductId() + "]";
  }
}
