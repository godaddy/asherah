package com.godaddy.asherah.appencryption;

public class DefaultPartition extends Partition {

  public DefaultPartition(final String partitionId, final String serviceId, final String productId) {
    super(partitionId, serviceId, productId);
  }

  @Override
  public String toString() {
    return getClass().getSimpleName() + "[partitionId=" + getPartitionId() +
      ", serviceId=" + getServiceId() + ", productId=" + getProductId() + "]";
  }
}
