use crate::util;

#[test]
fn test_exact_scenario() {
    println!("Test starting");

    // Reproduce the exact scenario from Buffer::new
    let size = 1usize;
    let page_size = *util::PAGE_SIZE;
    let user_data_len = size;
    let inner_region_total_len = util::round_to_page_size(user_data_len);
    let _actual_canary_len = inner_region_total_len - user_data_len;
    let total_alloc_len = (2 * page_size) + inner_region_total_len;

    println!("Page size: {}", page_size);
    println!("Total alloc len: {}", total_alloc_len);

    match memcall::allocate_aligned(total_alloc_len, page_size) {
        Ok(mem_ptr) => {
            println!("Allocated memory successfully");

            // Create Vec from raw parts
            println!("Creating Vec from raw parts");
            let mut allocation =
                unsafe { Vec::from_raw_parts(mem_ptr, total_alloc_len, total_alloc_len) };
            println!("Created Vec");

            // Define offsets
            let inner_offset = page_size;
            let inner_len = inner_region_total_len;

            // Try to lock inner region
            println!(
                "Trying to get inner slice from {} to {}",
                inner_offset,
                inner_offset + inner_len
            );
            let inner_slice = &mut allocation[inner_offset..(inner_offset + inner_len)];

            println!("Calling memcall::lock on inner slice");
            match memcall::lock(inner_slice) {
                Ok(()) => {
                    println!("Successfully locked inner region");

                    // Unlock
                    match memcall::unlock(inner_slice) {
                        Ok(()) => println!("Successfully unlocked"),
                        Err(e) => println!("Error unlocking: {:?}", e),
                    }
                }
                Err(e) => {
                    println!("Error locking inner region: {:?}", e);
                }
            }

            // Don't let Vec deallocate - free it manually
            let ptr = allocation.as_mut_ptr();
            let len = allocation.len();
            std::mem::forget(allocation); // Don't call the Vec destructor

            println!("Manually freeing aligned memory");
            unsafe {
                let result = memcall::free_aligned(ptr, len);
                println!("Free aligned result: {:?}", result);
            }
        }
        Err(e) => {
            println!("Error allocating memory: {:?}", e);
        }
    }

    println!("Test completed");
}
