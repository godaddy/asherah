use crate::globals;

#[test]
fn debug_test_issue_minimal() {
    println!("Starting minimal test");
    
    // Let's test the registry directly
    println!("Getting buffer registry");
    let registry = globals::get_buffer_registry();
    println!("Got registry, now locking");
    
    let registry_guard = registry.lock();
    println!("About to unwrap lock");
    match registry_guard {
        Ok(_reg) => {
            println!("Registry locked successfully");
            
            // Try to add a simple value
            println!("Registry locked successfully");
        }
        Err(e) => {
            println!("Error locking registry: {:?}", e);
        }
    }
    
    println!("Test completed");
}