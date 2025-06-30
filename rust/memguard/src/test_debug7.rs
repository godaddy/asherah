use crate::util;

#[test]
fn test_memcall_lock() {
    println!("Test starting");

    // Test memcall::lock directly
    let page_size = *util::PAGE_SIZE;
    let test_size = page_size;

    println!("Allocating memory");
    match memcall::allocate_aligned(test_size, page_size) {
        Ok(ptr) => {
            println!("Allocated memory successfully");

            // Create a slice from the ptr
            let slice = unsafe { std::slice::from_raw_parts_mut(ptr, test_size) };

            println!("Calling memcall::lock on slice of {} bytes", test_size);
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
