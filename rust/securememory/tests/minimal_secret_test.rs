use securememory::protected_memory::secret_simple::ProtectedMemorySecretSimple;
use securememory::secret::Secret;

#[test]
fn test_minimal_create_and_drop() {
    // env_logger::init();

    // Create a secret
    eprintln!("Creating secret...");
    let secret = ProtectedMemorySecretSimple::new(b"test data").unwrap();
    eprintln!("Secret created successfully");

    // Check length
    eprintln!("Checking length...");
    assert_eq!(secret.len(), 9);
    eprintln!("Length check passed");

    // Let it drop
    eprintln!("Dropping secret via drop method...");
    drop(secret);
    eprintln!("Secret dropped successfully");
}

#[test]
fn test_explicit_close() {
    // env_logger::init();

    // Create a secret
    eprintln!("Creating secret...");
    let secret = ProtectedMemorySecretSimple::new(b"test data").unwrap();
    eprintln!("Secret created successfully");

    // Check length
    eprintln!("Checking length...");
    assert_eq!(secret.len(), 9);
    eprintln!("Length check passed");

    // Explicitly close
    eprintln!("Closing secret...");
    secret.close().unwrap();
    eprintln!("Secret closed successfully");

    // Now drop
    eprintln!("Dropping closed secret...");
    drop(secret);
    eprintln!("Secret dropped successfully");
}

#[test]
fn test_without_memcall() {
    use memcall::{self};

    // Test if memcall is working properly
    let page_size = memcall::page_size();
    eprintln!("Page size: {}", page_size);

    // Try allocating aligned memory directly
    let ptr = memcall::allocate_aligned(page_size, page_size).unwrap();
    eprintln!("Allocated memory at: {:?}", ptr);

    // Create a vec from it
    let vec = unsafe { Vec::from_raw_parts(ptr, 10, page_size) };
    eprintln!(
        "Created vec with length: {}, capacity: {}",
        vec.len(),
        vec.capacity()
    );

    // Forget the vec to prevent double-free
    std::mem::forget(vec);

    // Free it manually
    unsafe {
        memcall::free_aligned(ptr, page_size).unwrap();
    }
    eprintln!("Freed aligned memory successfully");
}
