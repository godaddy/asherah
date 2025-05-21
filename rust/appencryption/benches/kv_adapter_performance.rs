use async_trait::async_trait;
use criterion::{black_box, criterion_group, criterion_main, Criterion};
use std::collections::HashMap;
use std::sync::{Arc, Mutex};
use tokio::runtime::Runtime;

use appencryption::envelope::{EnvelopeKeyRecord, KeyMeta};
use appencryption::metastore::kv_adapter::{KeyValueMetastoreForLocal, KeyValueMetastoreForSend};
use appencryption::metastore::kv_store::{KeyValueStoreLocal, KeyValueStoreSend};
use appencryption::{Error, Metastore, Result};

// Send-compatible key-value store
#[derive(Debug, Clone)]
struct SendKvStore {
    data: Arc<Mutex<HashMap<String, String>>>,
}

impl SendKvStore {
    fn new() -> Self {
        Self {
            data: Arc::new(Mutex::new(HashMap::new())),
        }
    }
}

#[async_trait]
impl KeyValueStoreSend for SendKvStore {
    type Key = String;
    type Value = String;
    type Error = Error;

    async fn get(&self, key: &Self::Key) -> Result<Option<Self::Value>> {
        let data = self.data.lock().unwrap();
        Ok(data.get(key).cloned())
    }

    async fn put(
        &self,
        key: &Self::Key,
        value: &Self::Value,
        only_if_absent: bool,
    ) -> Result<bool> {
        let mut data = self.data.lock().unwrap();
        if only_if_absent && data.contains_key(key) {
            Ok(false)
        } else {
            data.insert(key.clone(), value.clone());
            Ok(true)
        }
    }

    async fn delete(&self, key: &Self::Key) -> Result<bool> {
        let mut data = self.data.lock().unwrap();
        Ok(data.remove(key).is_some())
    }
}

// Non-Send key-value store (but compatible with spawn_blocking)
#[derive(Debug, Clone)]
struct LocalKvStore {
    data: Arc<Mutex<HashMap<String, String>>>,
}

impl LocalKvStore {
    fn new() -> Self {
        Self {
            data: Arc::new(Mutex::new(HashMap::new())),
        }
    }
}

#[async_trait(?Send)]
impl KeyValueStoreLocal for LocalKvStore {
    type Key = String;
    type Value = String;
    type Error = Error;

    async fn get(&self, key: &Self::Key) -> Result<Option<Self::Value>> {
        let data = self.data.lock().unwrap();
        Ok(data.get(key).cloned())
    }

    async fn put(
        &self,
        key: &Self::Key,
        value: &Self::Value,
        only_if_absent: bool,
    ) -> Result<bool> {
        let mut data = self.data.lock().unwrap();
        if only_if_absent && data.contains_key(key) {
            Ok(false)
        } else {
            data.insert(key.clone(), value.clone());
            Ok(true)
        }
    }

    async fn delete(&self, key: &Self::Key) -> Result<bool> {
        let mut data = self.data.lock().unwrap();
        Ok(data.remove(key).is_some())
    }
}

async fn bench_store_and_load<M: Metastore>(metastore: &M, id: &str, n: usize) -> Result<()> {
    for i in 0..n {
        let created = chrono::Utc::now().timestamp() + i as i64;
        let record = EnvelopeKeyRecord {
            id: format!("key_{}", i),
            revoked: None,
            created,
            encrypted_key: vec![i as u8; 32],
            parent_key_meta: Some(KeyMeta::new("parent".to_string(), created)),
        };

        metastore.store(id, created, &record).await?;
        let _loaded = metastore.load(id, created).await?;
    }
    Ok(())
}

fn bench_send_metastore(c: &mut Criterion) {
    let rt = Runtime::new().unwrap();

    c.bench_function("send_metastore_operations", |b| {
        b.to_async(&rt).iter(|| async {
            let kv_store = Arc::new(SendKvStore::new());
            let metastore = KeyValueMetastoreForSend::<_, String, String>::new(kv_store);

            bench_store_and_load(&metastore, "test_partition", black_box(10))
                .await
                .unwrap();
        });
    });
}

fn bench_local_metastore(c: &mut Criterion) {
    let rt = Runtime::new().unwrap();

    c.bench_function("local_metastore_operations", |b| {
        b.to_async(&rt).iter(|| async {
            let kv_store = Arc::new(LocalKvStore::new());
            let metastore = KeyValueMetastoreForLocal::<_, String, String>::new(kv_store);

            bench_store_and_load(&metastore, "test_partition", black_box(10))
                .await
                .unwrap();
        });
    });
}

fn bench_comparative_performance(c: &mut Criterion) {
    let rt = Runtime::new().unwrap();

    let mut group = c.benchmark_group("metastore_comparison");

    group.bench_function("send_adapter", |b| {
        b.to_async(&rt).iter(|| async {
            let kv_store = Arc::new(SendKvStore::new());
            let metastore = KeyValueMetastoreForSend::<_, String, String>::new(kv_store);

            bench_store_and_load(&metastore, "partition", black_box(5))
                .await
                .unwrap();
        });
    });

    group.bench_function("local_adapter_with_spawn_blocking", |b| {
        b.to_async(&rt).iter(|| async {
            let kv_store = Arc::new(LocalKvStore::new());
            let metastore = KeyValueMetastoreForLocal::<_, String, String>::new(kv_store);

            bench_store_and_load(&metastore, "partition", black_box(5))
                .await
                .unwrap();
        });
    });

    group.finish();
}

criterion_group!(
    benches,
    bench_send_metastore,
    bench_local_metastore,
    bench_comparative_performance
);
criterion_main!(benches);
