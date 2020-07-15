# Library of Testing

Below we describe the set of functional, regression and cross-language tests that are currently used.

* [Regression Tests](#regression-tests)
* [Multi-threaded Tests](#multi-threaded-tests)
* [Cross Language Testing Framework (CLTF)](#cross-language-testing-framework-cltf)
* [Future Work](#future-work)

## Regression Tests

### Sanity checks
- Simple encrypt/decrypt operation.
- Encrypt/decrypt operation multiple times in the same session.
- Encrypt a payload and try to decrypt the same payload in a different session. Ensure that the results match.
- Encrypt a payload using some partition id. Ensure that it cannot be decrypted using a different partition id.
- Encrypt two different payloads and verify that both can be decrypted individually.
- Simple store/load operation.
- Try to overwrite a payload with the same key, and verify that load still works with the same key and returns the 2nd payload.
- Try to load an invalid key and verify that no result is returned.

### Parameterized Tests
System and Intermediate keys in the cache and metastore can each exist in 3 possible states:
- ***RETIRED:*** A key is present, but it is expired or revoked.
- ***VALID:*** A key is present and can be used for encrypt/decrypt operations.
- ***EMPTY:*** The key does not exist.

Based on permutation of these states, we can have a total of 81 (3 ^ 4) combinations of the Intermediate Key (IK) and System Key (SK) in the cache and metastore.

|  Cache IK | Metastore IK  | Cache SK  | Metastore SK  |
| ------------ | ------------ | ------------ | ------------ |
| RETIRED  | RETIRED  | RETIRED  | RETIRED  |
| RETIRED  | RETIRED  | RETIRED  | VALID  |
| RETIRED  | RETIRED  | RETIRED  | EMPTY  |
| RETIRED  | RETIRED  | VALID  | RETIRED  |
... and so on.

For each state combination we encrypt and decrypt a payload, verifying that it runs successfully. Each state combination leads to a set of conditions and resulting interactions with the metastore. 

For the encrypt path:

| Condition  | Expected interactions on metastore  |
| ------------ | ------------ |
| IK should be stored  | `metastore.store(IK)`  |
| SK should be stored  | `metastore.store(SK)`  |
| Neither IK nor SK should be stored  | No call to `metastore.store()`  |
| IK should be loaded  | N/A as we don't read IK from metastore while encrypting  |
| SK should be loaded  | `metastore.load(SK)`  |
| Neither IK nor SK should not be loaded  | No call to `metastore.load()`  |
| Latest IK should be loaded  | `metastore.loadLatest(IK)`  |
| Latest SK should be loaded  | `metastore.loadLatest(SK)`  |
| Neither latest IK nor SK should be loaded  | No call to `metastore.loadLatest()`  |

For the decrypt path:

| Condition  | Expected interactions on metastore  |
| ------------ | ------------ |
| IK should be loaded  | `metastore.load(IK)`  |
| SK should be loaded  | `metastore.load(SK)`  |

## Multi-threaded Tests
- Run encrypt and decrypt operations with multiple threads.
- Create a single session to encrypt and decrypt data for multiple partitions in parallel.
- Create a single session and call store/load to store and load data for multiple partitions in parallel.
- Create multiple sessions from multiple factories to encrypt and decrypt data using different partition in each thread.
- Create multiple sessions from multiple factories using the same partition in multiple threads.

Ensure that all keys and data row records are created as expected.

## Cross-language Testing Framework (CLTF)
A cross-language testing framework has been implemented using [Gherkin/Cucumber](https://cucumber.io/docs/gherkin/). 
The CLTF validates inter-language operability between all supported languages by running the following features.

- Encrypt operation in all languages
- Decrypt operation for the encrypted payload generated in all languages

## Future Work
- Expand the testing framework by adding additional inter-platform features.
- Add test cases for queued key rotation when implemented.
