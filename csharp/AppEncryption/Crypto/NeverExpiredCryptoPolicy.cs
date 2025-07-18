using System;

namespace GoDaddy.Asherah.Crypto
{
  public class NeverExpiredCryptoPolicy : CryptoPolicy
  {
    /// <inheritdoc />
    public override bool IsKeyExpired(DateTimeOffset keyCreationDate)
    {
      return false;
    }

    /// <inheritdoc />
    public override long GetRevokeCheckPeriodMillis()
    {
      return long.MaxValue;
    }

    /// <inheritdoc />
    public override bool CanCacheSystemKeys()
    {
      return true;
    }

    /// <inheritdoc />
    public override bool CanCacheIntermediateKeys()
    {
      return true;
    }

    /// <inheritdoc />
    public override bool CanCacheSessions()
    {
      return false;
    }

    /// <inheritdoc />
    public override long GetSessionCacheMaxSize()
    {
      return long.MaxValue;
    }

    /// <inheritdoc />
    public override long GetSessionCacheExpireMillis()
    {
      return long.MaxValue;
    }

    /// <inheritdoc />
    public override bool NotifyExpiredIntermediateKeyOnRead()
    {
      return true;
    }

    /// <inheritdoc />
    public override bool NotifyExpiredSystemKeyOnRead()
    {
      return true;
    }

    /// <inheritdoc />
    public override KeyRotationStrategy GetKeyRotationStrategy()
    {
      return KeyRotationStrategy.Inline;
    }
  }
}
