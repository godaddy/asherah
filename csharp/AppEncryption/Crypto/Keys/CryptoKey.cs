using System;

namespace GoDaddy.Asherah.Crypto.Keys
{
  public abstract class CryptoKey : IDisposable
  {
    /// <summary>
    /// Get the created time of the <see cref="CryptoKey"/>.
    /// </summary>
    ///
    /// <returns>The created time of the CryptoKey.</returns>
    public abstract DateTimeOffset GetCreated();

    /// <summary>
    /// Performs an action with the <see cref="CryptoKey"/>.
    /// </summary>
    /// <param name="actionWithKey">The action to be performed.</param>
    public abstract void WithKey(Action<byte[]> actionWithKey);

    /// <summary>
    /// Applies a function to the key.
    /// </summary>
    ///
    /// <param name="actionWithKey">The function to execute.</param>
    /// <typeparam name="TResult">The type used to store the result of the function.</typeparam>
    /// <returns>The result of the function.</returns>
    public abstract TResult WithKey<TResult>(Func<byte[], TResult> actionWithKey);

    /// <inheritdoc />
    public abstract void Dispose();

    /// <summary>
    /// Checks if the <see cref="CryptoKey"/> is revoked.
    /// </summary>
    ///
    /// <returns><value>true</value> if the key is revoked, else <value>false</value>.</returns>
    public abstract bool IsRevoked();

    /// <summary>
    /// Marks the <see cref="CryptoKey"/> as revoked.
    /// </summary>
    public abstract void MarkRevoked();
  }
}
