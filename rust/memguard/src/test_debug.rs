use crate::Buffer;

#[test]
fn debug_test_api_typed_byte_array16_ptr() {
    println!("Starting test_api_typed_byte_array16_ptr debug");
    
    const N: usize = 16;
    // Case 1: Exact size
    println!("Creating buffer");
    let b_exact = Buffer::new(N).unwrap();
    
    println!("Filling buffer");
    b_exact.with_data_mut(|d| { 
        println!("Inside with_data_mut");
        d.fill(0xAA); 
        Ok(()) 
    }).unwrap();
    
    println!("Starting byte_array16_ptr");
    unsafe {
        b_exact.byte_array16_ptr(|ptr_opt| {
            println!("Inside byte_array16_ptr callback");
            assert!(ptr_opt.is_some());
            let arr_ref = ptr_opt.unwrap().as_ref().unwrap();
            assert_eq!(arr_ref[0], 0xAA);
            println!("Finished assertions");
        }).unwrap();
    }
    
    println!("Destroying buffer");
    b_exact.destroy().unwrap();
    
    println!("Test completed successfully");
}