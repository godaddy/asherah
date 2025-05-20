use std::sync::{Arc, Mutex};
use std::thread;
use std::time::Duration;

// Simulating the registry structure
struct Registry {
    buffers: Vec<Arc<Mutex<Buffer>>>,
}

struct Buffer {
    id: usize,
}

impl Buffer {
    fn destroy(&self, registry: Arc<Mutex<Registry>>) {
        println!("Buffer {} attempting to destroy", self.id);
        thread::sleep(Duration::from_millis(10)); // Simulate work
        
        // Try to remove self from registry
        let mut reg = registry.lock().unwrap();
        reg.buffers.retain(|b| {
            let buffer = b.lock().unwrap(); // POTENTIAL DEADLOCK: registry holds lock, trying to lock buffer
            buffer.id != self.id
        });
        println!("Buffer {} destroyed", self.id);
    }
}

fn main() {
    let registry = Arc::new(Mutex::new(Registry { buffers: vec![] }));
    
    // Create some buffers
    for i in 0..3 {
        let buffer = Arc::new(Mutex::new(Buffer { id: i }));
        registry.lock().unwrap().buffers.push(buffer.clone());
    }
    
    // Simulate concurrent destroy
    let mut handles = vec![];
    
    for i in 0..3 {
        let reg_clone = registry.clone();
        let handle = thread::spawn(move || {
            let buffer = {
                let reg = reg_clone.lock().unwrap();
                reg.buffers[i].clone()
            };
            
            let buffer_ref = buffer.lock().unwrap();
            buffer_ref.destroy(reg_clone);
        });
        handles.push(handle);
    }
    
    for handle in handles {
        handle.join().unwrap();
    }
}