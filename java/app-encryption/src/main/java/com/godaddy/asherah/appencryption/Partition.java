package com.godaddy.asherah.appencryption;

public abstract class Partition {

  private final String partitionId;
  private final String serviceId;
  private final String productId;

  protected Partition(final String partitionId, final String serviceId, final String productId) {
    this.partitionId = partitionId;
    this.serviceId = serviceId;
    this.productId = productId;
  }

  String getPartitionId() {
    return partitionId;
  }

  String getServiceId() {
    return serviceId;
  }

  String getProductId() {
    return productId;
  }

  /**
   * Gets the SystemKey Id associated with the partition
   * @return The System Key Id
   */
  public abstract String getSystemKeyId();

  /**
   * Gets the Intermediate Key Id associated with the partition
   * @return The IntermediateKey Id
   */
  public abstract String getIntermediateKeyId();
}
