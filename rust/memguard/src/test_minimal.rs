#[cfg(test)]
mod test_minimal {
    #[test]
    fn test_minimal_creation() {
        
        match super::super::Buffer::new(32) {
            Ok(buffer) => {
                buffer.destroy().unwrap();
            },
            Err(e) => {
                panic!("Buffer creation failed: {:?}", e);
            }
        }
        
    }
}