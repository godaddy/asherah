use std::time::Instant;

fn main() {
    // Simple benchmark test to confirm timing works
    let start = Instant::now();
    let mut sum = 0u64;
    for i in 0..1_000_000 {
        sum = sum.wrapping_add(i);
    }
    let elapsed = start.elapsed();
    println!("Sum: {} in {:?}", sum, elapsed);

    // Test current directory
    println!("Current dir: {:?}", std::env::current_dir());
}