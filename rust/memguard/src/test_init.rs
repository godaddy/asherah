#[cfg(test)]
mod test_init {
    #[test]
    fn test_static_initialization() {
        
        // Just try to trigger static initialization
        {
            println!("Hello from test");
        }
        
    }
}