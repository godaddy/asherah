# Crypto Policy

The crypto policy interface dictates the various behaviors of Asherah and can be configured with the below options:

* **canCacheSystemKeys**
  * enables/disables caching of System Keys
* **canCacheIntermediateKeys**
  * enables/disables caching of Intermediate Keys
* **getRevokeCheckPeriodMillis**
  * the time period to revoke keys present in cache
* **isKeyExpired**
  * defines if a key is expired
* **keyRotationStrategy**
  * defines the key rotation strategy; enumeration of INLINE, QUEUED.
* **notifyExpiredSystemKeyOnRead**
  * enables/disables notifications for expired System Key.
* **notifyExpiredIntermediateKeyOnRead**
  * enables/disables notifications for expired Intermediate Key.

## Implemented Policies

### Basic Expiring Crypto Policy

This policy has some suggested pre-configured defaults which can be optionally overridden. The two required properties 
for this policy are:

* **keyExpirationDays:** The number of days after which a key will expire. This is tied to the `isKeyExpired` interface 
method. If a key is expired, the old key is used to decrypt the data. Depending on the crypto policy configuration it is 
then rotated inline on the next write or queued for renewal (not implemented as of this writing).
* **revokeCheckMinutes:** Keys are cached to minimize calls to external datastores for fetching and decrypting keys. This 
property sets the cache's TTL and is needed in case we chose to revoke the keys in the cache. This is tied to the 
`getRevokeCheckPeriodMillis` interface method.

### Never Expiring Crypto Policy (FOR TESTING ONLY)

This policy supports keys that neither expire nor are removed from the cache. This ***should never be used in the 
production environment***.
