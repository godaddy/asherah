package com.godaddy.asherah.appencryption;

/**
 * An additional layer of abstraction which generates the system key and intermediate key ids.
 * It uses a {@code partitionId} to uniquely identify a {@link Session}, i.e. every partition id should have its own
 * session. A payload encrypted using some partition id, cannot be decrypted using a different one.
 */
public abstract class Partition {
  private final String partitionId;
  private final String serviceId;
  private final String productId;

  /**
   * Create a new {@code Partition} instance using the provided parameters.
   *
   * @param partitionId A unique identifier for a {@link Session}.
   * @param serviceId A unique identifier for a service, used to create a {@link SessionFactory} object.
   * @param productId A unique identifier for a product, used to create a {@link SessionFactory} object.
   */
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

  /**
  * Validates an {@code IntermediateKey} id.
  *
  * @param id The {@code IntermediateKey} id to validate.
  * @return Returns {@code true} if the {@code id} is a valid {@code IntermediateKey} id for this partition.
  */
  public boolean isValidIntermediateKeyId(final String id) {
    return id.equals(getIntermediateKeyId());
  }
}
