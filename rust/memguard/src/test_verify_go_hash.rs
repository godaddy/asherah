use base64::{engine::general_purpose::STANDARD, Engine};

#[test]
fn verify_go_test_values() {
    // Decode the expected base64 values from Go
    let test_b64 = "kosgNmlD4q/RHrwOri5TqTvxd6T885vMZNUDcE5l4gI=";
    let expected_bytes = STANDARD.decode(test_b64).unwrap();
    println!("Go expects: {:?}", expected_bytes);
    
    // Decode our base64
    let our_b64 = "kosgNmlD4q/RHrwOri5TqTvxd6T881vMZNUDcE5l4gI=";
    let our_bytes = STANDARD.decode(our_b64).unwrap();
    println!("We produce: {:?}", our_bytes);
}