use memguard::{Buffer, purge};

fn main() {
    println!("Creating buffer...");
    let buffer = Buffer::new(64).expect("Failed to create buffer");
    println!("Buffer created");

    println!("Destroying buffer...");
    buffer.destroy().expect("Failed to destroy");
    println!("Buffer destroyed");

    println!("Creating second buffer...");
    let buffer2 = Buffer::new(32).expect("Failed to create buffer");
    println!("Buffer2 created");

    println!("Calling purge...");
    purge();
    println!("Purge complete");

    println!("Checking if buffer2 is destroyed after purge...");
    let destroyed = buffer2.destroyed();
    println!("Buffer2 destroyed: {}", destroyed);
}