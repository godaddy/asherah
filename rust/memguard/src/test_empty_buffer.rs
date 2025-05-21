use crate::Buffer;

#[test]
fn test_empty_buffer_scramble() {
    println!("Creating empty buffer");
    let b = Buffer::new(0).unwrap();

    println!("Is alive? {}", b.is_alive());
    println!("Is destroyed? {}", b.destroyed());

    println!("Calling scramble");
    match b.scramble() {
        Ok(()) => println!("Scramble succeeded"),
        Err(e) => println!("Scramble failed: {:?}", e),
    }

    println!("Destroying buffer");
    b.destroy().unwrap();

    println!("Calling scramble on destroyed buffer");
    match b.scramble() {
        Ok(()) => println!("Scramble succeeded on destroyed"),
        Err(e) => println!("Scramble failed on destroyed: {:?}", e),
    }
}
