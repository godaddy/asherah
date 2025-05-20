use crate::util;

#[test]
fn test_exact_allocation() {
    println!("Test starting");
    
    // Reproduce exactly what Buffer::new does
    let size = 1;
    let page_size = *util::PAGE_SIZE;
    println!("Page size: {}", page_size);
    
    let user_data_len = size;
    let inner_region_total_len = util::round_to_page_size(user_data_len);
    println!("Inner region total len: {}", inner_region_total_len);
    
    let actual_canary_len = inner_region_total_len - user_data_len;
    println!("Canary len: {}", actual_canary_len);
    
    let total_alloc_len = (2 * page_size) + inner_region_total_len;
    println!("Total alloc len: {}", total_alloc_len);
    
    // Try the exact allocation
    println!("About to allocate {} bytes aligned to {}", total_alloc_len, page_size);
    match memcall::allocate_aligned(total_alloc_len, page_size) {
        Ok(ptr) => {
            println!("Successfully allocated memory");
            unsafe {
                if let Err(e) = memcall::free_aligned(ptr, total_alloc_len) {
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