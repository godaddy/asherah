use crate::util;

#[test]
fn test_minimal_issue() {
    println!("Test starting");

    // Just test the page size directly
    println!("Getting page size...");
    let page_size = *util::PAGE_SIZE;
    println!("Page size: {}", page_size);

    // Test memcall directly
    println!("About to allocate memory");
    let test_size = page_size; // Use actual page size instead of hardcoded 4096
    match memcall::allocate_aligned(test_size, page_size) {
        Ok(ptr) => {
            println!("Successfully allocated memory");
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
