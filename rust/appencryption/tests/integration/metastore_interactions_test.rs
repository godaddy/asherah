// Tests to verify correct metastore interactions during encryption/decryption

use crate::integration::common::{
    create_crypto, create_static_kms, create_test_config, ORIGINAL_DATA, PARTITION_ID, PRODUCT,
    SERVICE,
};
use appencryption::{
    envelope::EnvelopeKeyRecord, metastore::InMemoryMetastore, Error, Metastore, Session,
    SessionFactory,
};
use async_trait::async_trait;
use securememory::protected_memory::DefaultSecretFactory;
use std::fmt;
use std::sync::{
    atomic::{AtomicUsize, Ordering},
    Arc, Mutex,
};

#[derive(Debug, Clone)]
enum MetastoreOperation {
    Load { id: String, created: i64 },
    LoadLatest { id: String },
    Store { id: String, created: i64 },
}

impl fmt::Display for MetastoreOperation {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            MetastoreOperation::Load { id, created } => {
                write!(f, "Load(id={}, created={})", id, created)
            }
            MetastoreOperation::LoadLatest { id } => write!(f, "LoadLatest(id={})", id),
            MetastoreOperation::Store { id, created } => {
                write!(f, "Store(id={}, created={})", id, created)
            }
        }
    }
}

// A metastore wrapper that tracks all operations
struct TrackingMetastore<M: Metastore> {
    inner: Arc<M>,
    operations: Arc<Mutex<Vec<MetastoreOperation>>>,
    load_count: AtomicUsize,
    load_latest_count: AtomicUsize,
    store_count: AtomicUsize,
}

impl<M: Metastore> TrackingMetastore<M> {
    fn new(inner: Arc<M>) -> Self {
        Self {
            inner,
            operations: Arc::new(Mutex::new(Vec::new())),
            load_count: AtomicUsize::new(0),
            load_latest_count: AtomicUsize::new(0),
            store_count: AtomicUsize::new(0),
        }
    }

    fn get_operations(&self) -> Vec<MetastoreOperation> {
        self.operations.lock().unwrap().clone()
    }

    fn get_operation_counts(&self) -> (usize, usize, usize) {
        (
            self.load_count.load(Ordering::SeqCst),
            self.load_latest_count.load(Ordering::SeqCst),
            self.store_count.load(Ordering::SeqCst),
        )
    }
}

#[async_trait]
impl<M: Metastore> Metastore for TrackingMetastore<M> {
    async fn load(&self, id: &str, created: i64) -> Result<Option<EnvelopeKeyRecord>, Error> {
        self.load_count.fetch_add(1, Ordering::SeqCst);

        self.operations
            .lock()
            .unwrap()
            .push(MetastoreOperation::Load {
                id: id.to_string(),
                created,
            });

        self.inner.load(id, created).await
    }

    async fn load_latest(&self, id: &str) -> Result<Option<EnvelopeKeyRecord>, Error> {
        self.load_latest_count.fetch_add(1, Ordering::SeqCst);

        self.operations
            .lock()
            .unwrap()
            .push(MetastoreOperation::LoadLatest { id: id.to_string() });

        self.inner.load_latest(id).await
    }

    async fn store(
        &self,
        id: &str,
        created: i64,
        envelope: &EnvelopeKeyRecord,
    ) -> Result<bool, Error> {
        self.store_count.fetch_add(1, Ordering::SeqCst);

        self.operations
            .lock()
            .unwrap()
            .push(MetastoreOperation::Store {
                id: id.to_string(),
                created,
            });

        self.inner.store(id, created, envelope).await
    }
}

#[tokio::test]
async fn test_encrypt_metastore_interactions() {
    // Create dependencies
    let config = create_test_config();
    let _crypto = create_crypto();
    let kms = create_static_kms().await;

    // Create a basic metastore
    let inner_metastore = Arc::new(InMemoryMetastore::new());

    // Wrap it with our tracking metastore
    let tracking_metastore = Arc::new(TrackingMetastore::new(inner_metastore));

    // Create session factory (need to pass metastore as Arc<dyn Metastore>)
    let metastore: Arc<dyn Metastore> = tracking_metastore.clone();
    let policy = (*config.policy).clone();
    let factory = SessionFactory::new(
        config.service,
        config.product,
        policy,
        kms,
        metastore,
        Arc::new(DefaultSecretFactory::new()),
        vec![], // Empty options
    );

    // Get session
    let session = factory
        .session(PARTITION_ID)
        .await
        .expect("Failed to get session");

    // Reset counts
    let _ = tracking_metastore.get_operations();

    // Encrypt data for the first time - this should create a new DRK and store it
    let data = ORIGINAL_DATA.as_bytes().to_vec();
    let _drr = session
        .encrypt(&data)
        .await
        .expect("Failed to encrypt data");

    // Get operation counts
    let (_load_count, load_latest_count, store_count) = tracking_metastore.get_operation_counts();
    let operations = tracking_metastore.get_operations();

    // Check expected operations
    // For a first-time encryption, we expect:
    // 1. LoadLatest for Intermediate Key
    // 2. LoadLatest for System Key (parent)
    // 3. Store for System Key if it doesn't exist
    // 4. Store for Intermediate Key if it doesn't exist
    // 5. No direct Load operations
    assert_eq!(load_count, 0, "Should have no direct Load operations");
    assert!(
        load_latest_count >= 1,
        "Should have at least one LoadLatest operation"
    );
    assert!(store_count >= 1, "Should have at least one Store operation");

    // Verify the IK key ID format
    let expected_ik_id = format!("_IK_{}_{}_{}", PARTITION_ID, SERVICE, PRODUCT);

    // Check for operations referencing the IK
    let has_ik_operations = operations.iter().any(|op| match op {
        MetastoreOperation::LoadLatest { id } => id == &expected_ik_id,
        MetastoreOperation::Store { id, .. } => id == &expected_ik_id,
        _ => false,
    });

    assert!(has_ik_operations, "Should have operations for IK");
}

#[tokio::test]
async fn test_decrypt_metastore_interactions() {
    // Create dependencies
    let config = create_test_config();
    let _crypto = create_crypto();
    let kms = create_static_kms().await;

    // Create a basic metastore
    let inner_metastore = Arc::new(InMemoryMetastore::new());

    // Create a session factory directly with the inner metastore first
    let policy_clone = (*config.policy).clone();
    let inner_factory = SessionFactory::new(
        config.service.clone(),
        config.product.clone(),
        policy_clone,
        kms.clone(),
        inner_metastore.clone(),
        Arc::new(DefaultSecretFactory::new()),
        vec![], // Empty options
    );

    // Get session and encrypt some data to have a valid DRR for testing
    let inner_session = inner_factory
        .session(PARTITION_ID)
        .await
        .expect("Failed to get session");

    let data = ORIGINAL_DATA.as_bytes().to_vec();
    let _drr = inner_session
        .encrypt(&data)
        .await
        .expect("Failed to encrypt data");

    // Now create a tracking metastore wrapping the inner one
    let tracking_metastore = Arc::new(TrackingMetastore::new(inner_metastore));

    // Create a new session factory with the tracking metastore
    let policy = (*config.policy).clone();
    let metastore: Arc<dyn Metastore> = tracking_metastore.clone();
    let factory = SessionFactory::new(
        config.service,
        config.product,
        policy,
        kms,
        metastore,
        Arc::new(DefaultSecretFactory::new()),
        vec![], // Empty options
    );

    // Get session
    let session = factory
        .session(PARTITION_ID)
        .await
        .expect("Failed to get session");

    // Reset counts
    let _ = tracking_metastore.get_operations();

    // Decrypt the data
    let decrypted = session.decrypt(&drr).await.expect("Failed to decrypt data");

    // Check decryption was successful
    assert_eq!(ORIGINAL_DATA.as_bytes(), decrypted.as_slice());

    // Get operation counts
    let (_load_count, load_latest_count, store_count) = tracking_metastore.get_operation_counts();
    let operations = tracking_metastore.get_operations();

    // Check expected operations
    // For decryption, we expect:
    // 1. Load for the Intermediate Key (using the parentKeyMeta from the DRR)
    // 2. Load or LoadLatest for the System Key (parent of IK)
    // 3. No Store operations
    assert!(
        load_count + load_latest_count >= 1,
        "Should have at least one Load/LoadLatest operation"
    );
    assert_eq!(
        store_count, 0,
        "Should not have any Store operations during decrypt"
    );

    // Verify the IK key ID format
    let expected_ik_id = format!("_IK_{}_{}_{}", PARTITION_ID, SERVICE, PRODUCT);

    // Check for operations referencing the IK
    let has_ik_operations = operations.iter().any(|op| match op {
        MetastoreOperation::Load { id, .. } => id == &expected_ik_id,
        MetastoreOperation::LoadLatest { id } => id == &expected_ik_id,
        _ => false,
    });

    assert!(has_ik_operations, "Should have load operations for IK");
}

#[tokio::test]
async fn test_metastore_caching_behavior() {
    // Create dependencies
    let config = create_test_config();
    let _crypto = create_crypto();
    let kms = create_static_kms().await;

    // Create a basic metastore
    let inner_metastore = Arc::new(InMemoryMetastore::new());

    // Wrap it with our tracking metastore
    let tracking_metastore = Arc::new(TrackingMetastore::new(inner_metastore));

    // Create a session factory
    let policy = (*config.policy).clone();
    let metastore: Arc<dyn Metastore> = tracking_metastore.clone();
    let factory = SessionFactory::new(
        config.service,
        config.product,
        policy,
        kms,
        metastore,
        Arc::new(DefaultSecretFactory::new()),
        vec![], // Empty options
    );

    // Get session
    let session = factory
        .session(PARTITION_ID)
        .await
        .expect("Failed to get session");

    // Reset counts
    let _ = tracking_metastore.get_operations();

    // Encrypt data - first time should do metastore operations
    let data = ORIGINAL_DATA.as_bytes().to_vec();
    let _drr = session
        .encrypt(&data)
        .await
        .expect("Failed to encrypt data");

    // Get operation counts after first encryption
    let (load_count1, load_latest_count1, store_count1) = tracking_metastore.get_operation_counts();

    // Print the operations to see what's happening
    let operations = tracking_metastore.get_operations();
    println!("First encrypt operations: {:?}", operations);

    // Encrypt again - should use cached keys and do fewer metastore operations
    let _drr2 = session
        .encrypt(&data)
        .await
        .expect("Failed to encrypt data");

    // Get operation counts after second encryption
    let (load_count2, load_latest_count2, store_count2) = tracking_metastore.get_operation_counts();

    let operations2 = tracking_metastore.get_operations();
    println!("Second encrypt operations: {:?}", operations2);

    // Debug output
    println!(
        "First encrypt - load: {}, load_latest: {}, store: {}",
        load_count1, load_latest_count1, store_count1
    );
    println!(
        "Second encrypt - load: {}, load_latest: {}, store: {}",
        load_count2, load_latest_count2, store_count2
    );

    // We expect fewer operations on second encrypt due to caching
    assert!(
        load_count2 + load_latest_count2 <= load_count1 + load_latest_count1,
        "Second encrypt should have fewer or equal load operations"
    );

    assert!(
        store_count2 <= store_count1,
        "Second encrypt should have fewer or equal store operations"
    );
}
