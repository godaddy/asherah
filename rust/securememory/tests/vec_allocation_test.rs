use memcall::{allocate_aligned, free_aligned, page_size};

#[test]
fn test_vector_allocation_and_free() {
    println!("Starting vector allocation test");

    unsafe {
        let ps = page_size();
        println!("Page size: {}", ps);

        let size = 32;
        let aligned_size = ((size + ps - 1) / ps) * ps;
        println!("size: {}, aligned_size: {}", size, aligned_size);

        // Allocate memory
        let ptr = allocate_aligned(aligned_size, ps).unwrap();
        println!("Allocated at {:p}", ptr);

        // Create a vector from the raw parts
        let mut vec = Vec::from_raw_parts(ptr, size, aligned_size);
        println!(
            "Created vector with len={}, capacity={}",
            vec.len(),
            vec.capacity()
        );

        // Write data
        for i in 0..size {
            vec[i] = i as u8;
        }
        println!("Wrote data");

        // Get the pointer and capacity
        let ptr = vec.as_mut_ptr();
        let cap = vec.capacity();
        println!("About to free: ptr={:p}, cap={}", ptr, cap);

        // Prevent the vector from deallocating
        std::mem::forget(vec);

        // Free the memory
        let result = free_aligned(ptr, cap);
        println!("Free result: {:?}", result);
    }

    println!("Test complete");
}

#[test]
fn test_simple_aligned_alloc() {
    println!("Starting simple aligned allocation test");

    unsafe {
        let ps = page_size();
        let size = ps; // Exactly one page

        // Allocate
        let ptr = allocate_aligned(size, ps).unwrap();
        println!("Allocated {} bytes at {:p}", size, ptr);

        // Free
        let result = free_aligned(ptr, size);
        println!("Free result: {:?}", result);

        assert!(result.is_ok());
    }

    println!("Simple test complete");
}
