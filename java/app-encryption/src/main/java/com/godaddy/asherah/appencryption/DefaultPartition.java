package com.godaddy.asherah.appencryption;

public class DefaultPartition extends Partition {

  /**
   * Constructor for DefaultPartition.
   *
   * @param partitionId The partition id.
   * @param serviceId The service id.
   * @param productId The product id.
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
