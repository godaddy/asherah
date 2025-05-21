# Metastore Module

This module provides persistent storage implementations for encrypted keys in Asherah.

## Key-Value Store Architecture

The metastore module uses a dual-trait approach to handle both `Send` and non-`Send` futures in async contexts:

### Trait Hierarchy

1. **KeyValueStoreSend** - Requires futures to be `Send`
   - For implementations that can guarantee their futures are `Send`-compatible
   - Suitable for use in multi-threaded async runtimes (e.g., Tokio with multiple threads)

2. **KeyValueStoreLocal** - Doesn't require futures to be `Send`
   - For implementations that use stack references or other non-`Send` data across await points
   - Suitable for single-threaded async runtimes or when used with adapters

3. **TTL Variants** - Both traits have TTL-supporting extensions:
   - `TtlKeyValueStoreSend` extends `KeyValueStoreSend`
   - `TtlKeyValueStoreLocal` extends `KeyValueStoreLocal`

### Adapter Types

1. **KeyValueMetastoreForSend**
   - Adapts any `KeyValueStoreSend` implementation to the `Metastore` trait
   - Futures are already `Send`, so it can directly await them

2. **KeyValueMetastoreForLocal**
   - Adapts any `KeyValueStoreLocal` implementation to the `Metastore` trait
   - Uses `tokio::task::spawn_blocking` to safely execute non-`Send` futures in a dedicated thread, making them compatible with `Metastore`'s `Send` requirements

3. **SendKeyValueStoreAdapter** / **SendTtlKeyValueStoreAdapter**
   - Adapt any `KeyValueStoreLocal` / `TtlKeyValueStoreLocal` implementation to the `KeyValueStoreSend` / `TtlKeyValueStoreSend` traits
   - Uses `tokio::task::spawn_blocking` to safely execute non-`Send` futures in a dedicated thread, making them `Send`-compatible

### Type Aliases (For Backward Compatibility)

- `KeyValueStore` → `dyn KeyValueStoreSend`
- `TtlKeyValueStore` → `dyn TtlKeyValueStoreSend`
- `LocalKeyValueStore` → `dyn KeyValueStoreLocal`
- `LocalTtlKeyValueStore` → `dyn TtlKeyValueStoreLocal`
- `KeyValueMetastore` → `KeyValueMetastoreForSend`
- `StringKeyValueMetastore` → `StringKeyValueMetastoreForSend`

## Usage Guide

### For Library Users

The choice between Send and Local variants depends on your use case:

1. **If using a multi-threaded async runtime (common case):**
   - Use `KeyValueMetastoreForSend` for implementing `Metastore`
   - Implement `KeyValueStoreSend` for your key-value store

2. **If using stack references or other non-Send data in async code:**
   - Use `KeyValueMetastoreForLocal` for implementing `Metastore`
   - Implement `KeyValueStoreLocal` for your key-value store

3. **If you have a `KeyValueStoreLocal` implementation but need Send capabilities:**
   - Wrap it with `SendKeyValueStoreAdapter` to convert it to a `KeyValueStoreSend`
   - Then use with `KeyValueMetastoreForSend`

### For Library Implementers

When implementing a key-value store:

1. **If your store can provide Send futures:**
   ```rust
   #[async_trait]
   impl KeyValueStoreSend for MyStore {
       async fn get(&self, key: &Self::Key) -> Result<Option<Self::Value>, Self::Error> {
           // Implementation that produces Send futures
       }
       // ...other methods
   }
   ```

2. **If your store has non-Send futures:**
   ```rust
   #[async_trait(?Send)]
   impl KeyValueStoreLocal for MyStore {
       async fn get(&self, key: &Self::Key) -> Result<Option<Self::Value>, Self::Error> {
           // Implementation that may produce non-Send futures
       }
       // ...other methods
   }
   ```

3. **If you want to support both:**
   ```rust
   // Implement both traits, or use adapters to convert between them
   ```

## Understanding Send and Futures

The main difference between `KeyValueStoreSend` and `KeyValueStoreLocal` is whether the futures returned by their methods implement the `Send` trait. This affects where and how these futures can be used:

- **Send futures** can be moved between threads and are required when using multi-threaded async runtimes.
- **Non-Send futures** can't be moved between threads and must be executed on the same thread where they were created.

The `async_trait` macro used in Asherah transforms async methods in traits into ones that return `Pin<Box<dyn Future<Output = T> + Send>>` by default, requiring the futures to be `Send`. The `?Send` variant removes this requirement but limits where the trait can be used.

### Bridging Non-Send Futures

To bridge the gap between `Send` and non-`Send` futures, our adapters use `tokio::task::spawn_blocking`, which:

1. Creates a dedicated thread for executing blocking code
2. Gets the current tokio runtime within that thread
3. Uses `block_on` to execute the non-Send future within that dedicated thread
4. Returns a result that can be safely awaited from anywhere

This approach avoids the pitfalls of other methods:
- Unlike `SendWrapper`, it properly handles futures that need to be awaited
- Unlike thread-local storage, it works reliably across await points
- Unlike blocking executors, it integrates properly with the tokio runtime

The tradeoff is a small performance cost from thread creation, but it enables seamless interoperability between different types of futures.

This dual-trait approach provides flexibility while ensuring proper compile-time guarantees about thread safety.