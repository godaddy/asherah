#[cfg(test)]
mod test_single_thread {
    #[test]
    fn test_single_thread_buffer_creation() {
        match super::super::Buffer::new(32) {
            Ok(buffer) => {
                buffer.destroy().unwrap();
            }
            Err(e) => {
                panic!("Buffer creation failed: {:?}", e);
            }
        }
    }
}
