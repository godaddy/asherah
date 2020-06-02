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

  public String getSystemKeyId() {
    return "_SK_" + getServiceId() + "_" + getProductId();
  }

  public String getIntermediateKeyId() {
    return "_IK_" + getPartitionId() + "_" + getServiceId() + "_" + getProductId();
  }
}
