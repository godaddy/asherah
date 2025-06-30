use memcall::{alloc, disable_core_dumps, free, lock, protect, unlock, MemoryProtection};

/// This test directly mirrors the TestCycle test in the Go implementation.
#[test]
fn test_cycle() {
    // Allocate 32 bytes, just like in Go
    let buffer = alloc(32).expect("Failed to allocate memory");

    // Check buffer size matches what was requested
    assert_eq!(buffer.len(), 32, "allocation has invalid size");

    // Verify memory is zeroed
    for &byte in buffer.iter() {
        assert_eq!(byte, 0, "allocated memory not zeroed");
    }

    // Lock the memory
    lock(buffer).expect("Failed to lock memory");

    // Modify and check the buffer
    for byte in buffer.iter_mut() {
        *byte = 1;
        assert_eq!(*byte, 1, "read back data different to what was written");
    }

    // Unlock and free the memory
    unlock(buffer).expect("Failed to unlock memory");
    free(buffer).expect("Failed to free memory");

    // Disable core dumps
    disable_core_dumps().expect("Failed to disable core dumps");
}

/// This test directly mirrors the TestProtect test in the Go implementation.
#[test]
fn test_protect() {
    // Allocate memory
    let buffer = alloc(32).expect("Failed to allocate memory");

    // Test all protection modes
    protect(buffer, MemoryProtection::ReadWrite).expect("Failed to set ReadWrite protection");
    protect(buffer, MemoryProtection::ReadOnly).expect("Failed to set ReadOnly protection");
    protect(buffer, MemoryProtection::NoAccess).expect("Failed to set NoAccess protection");

    // Test invalid flag case
    // In Go this is: if err := Protect(buffer, MemoryProtectionFlag{4}); err.Error() != ErrInvalidFlag {
    // This creates an invalid flag with value 4, which should return an error containing ErrInvalidFlag
    let invalid_flag_error = protect(buffer, unsafe {
        std::mem::transmute::<u32, MemoryProtection>(4_u32)
    })
    .expect_err("Should have failed with invalid flag");

    // Error message should match ERR_INVALID_FLAG from Go
    assert!(invalid_flag_error.to_string().contains("<memcall> memory protection flag is undefined"),
            "Expected error message containing '<memcall> memory protection flag is undefined', got: {}",
            invalid_flag_error);

    // Free the memory
    free(buffer).expect("Failed to free memory");
}

/// This test directly mirrors the TestProtFlags test in the Go implementation.
#[test]
fn test_prot_flags() {
    // Verify memory protection flag values match the Go implementation
    assert_eq!(
        MemoryProtection::NoAccess as u32,
        1,
        "NoAccess value is incorrect"
    );
    assert_eq!(
        MemoryProtection::ReadOnly as u32,
        2,
        "ReadOnly value is incorrect"
    );
    assert_eq!(
        MemoryProtection::ReadWrite as u32,
        6,
        "ReadWrite value is incorrect"
    );
}

/// This is a Rust-specific test to ensure the page_size function works properly.
#[test]
fn test_page_size() {
    // Just verify that page_size returns a reasonable value
    let size = memcall::page_size();
    assert!(size > 0, "Page size should be greater than zero");
    assert!(size.is_power_of_two(), "Page size should be a power of 2");
}
