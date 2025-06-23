use crate::util;
use memcall::MemoryProtection;

#[test]
fn test_lock_with_permissions() {
    println!("Test starting");

    let page_size = *util::PAGE_SIZE;
    let test_size = page_size;

    match memcall::allocate_aligned(test_size, page_size) {
        Ok(ptr) => {
            println!("Allocated memory successfully");

            // Create a slice from the ptr
            let slice = unsafe { std::slice::from_raw_parts_mut(ptr, test_size) };

            // First, set memory to ReadWrite
            println!("Setting memory to ReadWrite");
            match memcall::protect(slice, MemoryProtection::ReadWrite) {
                Ok(()) => println!("Successfully set to ReadWrite"),
                Err(e) => println!("Error setting to ReadWrite: {:?}", e),
            }

            // Now try to lock it
            println!("Calling memcall::lock");
            match memcall::lock(slice) {
                Ok(()) => {
                    println!("Successfully locked memory");

                    // Try to unlock
                    match memcall::unlock(slice) {
                        Ok(()) => println!("Successfully unlocked memory"),
                        Err(e) => println!("Error unlocking memory: {:?}", e),
                    }
                }
                Err(e) => {
                    println!("Error locking memory: {:?}", e);
                }
            }

            unsafe {
                if let Err(e) = memcall::free_aligned(ptr, test_size) {
                    println!("Error freeing memory: {:?}", e);
                }
            }
        }
        Err(e) => {
            println!("Error allocating memory: {:?}", e);
        }
    }

    println!("Test completed");
}
