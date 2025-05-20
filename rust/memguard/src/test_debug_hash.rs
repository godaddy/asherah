use base64::encode;
use blake2::digest::consts::U32;
use blake2::{Blake2b, Digest};

#[test]
fn debug_hash_values() {
    type Blake2b256 = Blake2b<U32>;

    // Test empty string
    let mut hasher = Blake2b256::new();
    hasher.update(b"");
    let result = hasher.finalize();
    let base64_result = encode(result);
    println!("Empty string hash: {}", base64_result);
    println!("Expected: DldRwCblQ7Loqy6wYJnaodHl30d3j3eH+qtFzfEv46g=");

    // Test "hash"
    let mut hasher = Blake2b256::new();
    hasher.update(b"hash");
    let result = hasher.finalize();
    let base64_result = encode(result);
    println!("'hash' hash: {}", base64_result);
    println!("Expected: l+2qaVlkOBNtzRKFU+kEvAP1JkJvcn0nC2mEH7bPUNM=");

    // Test "test"
    let mut hasher = Blake2b256::new();
    hasher.update(b"test");
    let result = hasher.finalize();
    let base64_result = encode(result);
    println!("'test' hash: {}", base64_result);
    println!("Expected: kosgNmlD4q/RHrwOri5TqTvxd6T885vMZNUDcE5l4gI=");
}
