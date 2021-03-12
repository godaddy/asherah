package com.godaddy.asherah.appencryption;

public class DefaultPartition extends Partition {

  /**
   * Create a new {@code DefaultPartition} instance using the provided parameters.
   *
   * @param partitionId A unique identifier for a {@link Session}.
   * @param serviceId A unique identifier for a service, used to create a {@link SessionFactory} object.
   * @param productId A unique identifier for a product, used to create a {@link SessionFactory} object.
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
