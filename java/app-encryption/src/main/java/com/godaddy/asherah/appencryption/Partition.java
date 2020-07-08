package com.godaddy.asherah.appencryption;

public class Partition {

  private final String partitionId;
  private final String serviceId;
  private final String productId;

  public Partition(final String partitionId, final String serviceId, final String productId) {
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

  public String getSystemKeyId() {
    return "_SK_" + serviceId + "_" + productId;
  }

  public String getIntermediateKeyId() {
    return "_IK_" + partitionId + "_" + serviceId + "_" + productId;
  }

  @Override
  public String toString() {
    return getClass().getSimpleName() + "[partitionId=" + partitionId +
      ", serviceId=" + serviceId + ", productId=" + productId + "]";
  }
}
