use crate::envelope::DataRowRecord;
use crate::error::Result;
use crate::{Loader, Storer};

use async_trait::async_trait;
use std::marker::PhantomData;

/// A function adapter for implementing the Loader trait
pub struct LoaderFn<K, F>
where
    F: for<'key> Fn(&'key K) -> Result<Option<DataRowRecord>> + Send + Sync,
    K: Send + Sync,
{
    /// The load function
    f: F,
    /// Phantom type for the key
    _key_type: PhantomData<K>,
}

impl<K, F> LoaderFn<K, F>
where
    F: for<'key> Fn(&'key K) -> Result<Option<DataRowRecord>> + Send + Sync,
    K: Send + Sync,
{
    /// Creates a new LoaderFn with the given function
    pub fn new(f: F) -> Self {
        Self {
            f,
            _key_type: PhantomData,
        }
    }
}

#[async_trait]
impl<K, F> Loader for LoaderFn<K, F>
where
    F: for<'key> Fn(&'key K) -> Result<Option<DataRowRecord>> + Send + Sync,
    K: Send + Sync,
{
    type Key = K;

    async fn load(&self, key: &Self::Key) -> Result<Option<DataRowRecord>> {
        (self.f)(key)
    }
}

/// A function adapter for implementing the Storer trait
pub struct StorerFn<K, F>
where
    F: Fn(&DataRowRecord) -> Result<K> + Send + Sync,
    K: Send + Sync,
{
    /// The store function
    f: F,
    /// Phantom type for the key
    _key_type: PhantomData<K>,
}

impl<K, F> StorerFn<K, F>
where
    F: Fn(&DataRowRecord) -> Result<K> + Send + Sync,
    K: Send + Sync,
{
    /// Creates a new StorerFn with the given function
    pub fn new(f: F) -> Self {
        Self {
            f,
            _key_type: PhantomData,
        }
    }
}

#[async_trait]
impl<K, F> Storer for StorerFn<K, F>
where
    F: Fn(&DataRowRecord) -> Result<K> + Send + Sync,
    K: Send + Sync,
{
    type Key = K;

    async fn store(&self, drr: &DataRowRecord) -> Result<Self::Key> {
        (self.f)(drr)
    }
}
