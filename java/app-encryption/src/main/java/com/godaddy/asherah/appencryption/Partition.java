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
   * Get the system key id.
   *
   * @return The {@code SystemKey} id as a string.
   */
  public String getSystemKeyId() {
    return "_SK_" + serviceId + "_" + productId;
  }

  /**
   * Get the intermediate key id.
   *
   * @return The {@code IntermediateKey} id as a string.
   */
  public String getIntermediateKeyId() {
    return "_IK_" + partitionId + "_" + serviceId + "_" + productId;
  }
}
