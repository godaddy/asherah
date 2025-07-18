namespace GoDaddy.Asherah.AppEncryption
{
  /// <summary>
  /// The default implementation of <see cref="Partition"/>.
  /// </summary>
  public class DefaultPartition : Partition
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultPartition"/> class using the provided parameters.
    /// </summary>
    ///
    /// <param name="partitionId">A unique identifier for a <see cref="Session{TP,TD}"/>.</param>
    /// <param name="serviceId">A unique identifier for a service, used to create a <see cref="SessionFactory"/>
    /// object.</param>
    /// <param name="productId">A unique identifier for a product, used to create a <see cref="SessionFactory"/>
    /// object.</param>
    public DefaultPartition(string partitionId, string serviceId, string productId)
        : base(partitionId, serviceId, productId)
    {
    }

    /// <inheritdoc/>
    public override string ToString()
    {
      return GetType().Name + "[partitionId=" + PartitionId +
             ", serviceId=" + ServiceId + ", productId=" + ProductId + "]";
    }
  }
}
