use crate::error::MemcallError;
use crate::types::{MemoryProtection, RlimitResource};
use once_cell::sync::Lazy;
use std::ptr;

static PAGE_SIZE: Lazy<usize> = Lazy::new(|| unsafe {
    // Convert from libc::c_long to usize, which is necessary but not trivial
    let page_size = libc::sysconf(libc::_SC_PAGESIZE);
    // This conversion is safe because page sizes are always positive and much smaller than usize::MAX
    page_size.try_into().unwrap_or(4096) // Default to 4096 if conversion fails
});

#[inline]
fn as_mut_ptr(memory: &mut [u8]) -> *mut libc::c_void {
    // This cast is required for FFI compatibility with libc
    memory.as_mut_ptr().cast::<libc::c_void>()
}

#[inline]
fn as_len(memory: &[u8]) -> libc::size_t {
    // This conversion is necessary for FFI compatibility
    // size_t is always large enough to hold any slice length on supported platforms
    #[allow(trivial_numeric_casts)]
    {
        memory.len() as libc::size_t
    }
}

/// Allocates a new memory region with the given size and accessibility.
/// This function directly mirrors the behavior of the Go implementation.
pub fn alloc(size: usize) -> Result<&'static mut [u8], MemcallError> {
    // Allocate memory with mmap - directly matching the Go implementation
    let ptr = unsafe {
        libc::mmap(
            ptr::null_mut(),
            size,
            libc::PROT_READ | libc::PROT_WRITE,
            libc::MAP_PRIVATE | libc::MAP_ANON,
            -1,
            0,
        )
    };

    if ptr == libc::MAP_FAILED {
        return Err(MemcallError::SystemError(format!(
            "<memcall> could not allocate [Err: {}]",
            std::io::Error::last_os_error()
        )));
    }

    // Create a slice from the allocated memory
    // This cast is necessary because mmap returns void* which must be cast to u8*
    // for Rust's slice types. This is safe because we're just reinterpreting the
    // raw memory pointer for our specific use case.
    let memory = unsafe { std::slice::from_raw_parts_mut(ptr.cast::<u8>(), size) };

    // Wipe it just in case there is some remnant data - exactly as Go does
    for byte in memory.iter_mut() {
        *byte = 0;
    }

    Ok(memory)
}

/// Frees a memory region previously allocated with `alloc`.
/// This function directly mirrors the behavior of the Go implementation.
pub fn free(ptr: &mut [u8]) -> Result<(), MemcallError> {
    if ptr.is_empty() {
        return Ok(());
    }

    // Make the memory region readable and writable - exactly as Go does
    protect(ptr, MemoryProtection::ReadWrite)?;

    // Wipe the memory for security - exactly as Go does
    for byte in ptr.iter_mut() {
        *byte = 0;
    }

    // Free the memory with munmap
    let result = unsafe { libc::munmap(as_mut_ptr(ptr), as_len(ptr)) };

    if result != 0 {
        return Err(MemcallError::SystemError(format!(
            "<memcall> could not deallocate {:p} [Err: {}]",
            ptr.as_ptr(),
            std::io::Error::last_os_error()
        )));
    }

    Ok(())
}

// Error message for invalid flag, exactly matches Go's implementation
pub(crate) const ERR_INVALID_FLAG: &str = "<memcall> memory protection flag is undefined";

/// Changes memory protection for the specified region.
/// This function directly mirrors the behavior of the Go implementation.
pub fn protect(ptr: &mut [u8], protection: MemoryProtection) -> Result<(), MemcallError> {
    if ptr.is_empty() {
        return Ok(());
    }

    // Mimic Go's protection flag handling exactly, including checks for invalid flags
    // Use the numeric value from the enum safely
    let prot = match u32::from(protection) {
        1 => libc::PROT_NONE,                    // NoAccess
        2 => libc::PROT_READ,                    // ReadOnly
        6 => libc::PROT_READ | libc::PROT_WRITE, // ReadWrite
        _ => return Err(MemcallError::InvalidArgument(ERR_INVALID_FLAG.to_string())),
    };

    let result = unsafe { libc::mprotect(as_mut_ptr(ptr), as_len(ptr), prot) };

    if result != 0 {
        return Err(MemcallError::SystemError(format!(
            "<memcall> could not set {} on {:p} [Err: {}]",
            prot,
            ptr.as_ptr(),
            std::io::Error::last_os_error()
        )));
    }

    Ok(())
}

/// Locks a memory region to prevent it from being swapped to disk.
/// This function directly mirrors the behavior of the Go implementation.
pub fn lock(ptr: &mut [u8]) -> Result<(), MemcallError> {
    if ptr.is_empty() {
        return Ok(());
    }

    // The Go implementation only calls MADV_DONTDUMP on Linux, so we should do the same
    #[cfg(target_os = "linux")]
    unsafe {
        // Advise the kernel not to dump this memory (this call is identical to Go's)
        libc::madvise(as_mut_ptr(ptr), as_len(ptr), libc::MADV_DONTDUMP);
    }

    // Call mlock, exactly as in Go
    let result = unsafe { libc::mlock(as_mut_ptr(ptr), as_len(ptr)) };

    if result != 0 {
        return Err(MemcallError::SystemError(format!(
            "<memcall> could not acquire lock on {:p}, limit reached? [Err: {}]",
            ptr.as_ptr(),
            std::io::Error::last_os_error()
        )));
    }

    Ok(())
}

/// Unlocks a memory region previously locked with `lock`.
/// This function directly mirrors the behavior of the Go implementation.
pub fn unlock(ptr: &mut [u8]) -> Result<(), MemcallError> {
    if ptr.is_empty() {
        return Ok(());
    }

    // Call munlock, exactly as in Go
    let result = unsafe { libc::munlock(as_mut_ptr(ptr), as_len(ptr)) };

    if result != 0 {
        return Err(MemcallError::SystemError(format!(
            "<memcall> could not free lock on {:p} [Err: {}]",
            ptr.as_ptr(),
            std::io::Error::last_os_error()
        )));
    }

    Ok(())
}

/// Returns the system's page size.
pub fn page_size() -> usize {
    *PAGE_SIZE
}

/// Disables creation of core dump files for the current process.
/// This function directly mirrors the behavior of the Go implementation.
pub fn disable_core_dumps() -> Result<(), MemcallError> {
    // Create the resource limit structure with zeros
    let rlimit = libc::rlimit {
        rlim_cur: 0,
        rlim_max: 0,
    };

    // Set the core dump resource limit to zero
    let result = unsafe { libc::setrlimit(libc::RLIMIT_CORE, &rlimit) };

    if result != 0 {
        return Err(MemcallError::SystemError(format!(
            "<memcall> could not set rlimit [Err: {}]",
            std::io::Error::last_os_error()
        )));
    }

    Ok(())
}

/// Sets a resource limit for the current process.
pub fn set_limit(resource: RlimitResource, value: u64) -> Result<(), MemcallError> {
    let resource_id = match resource {
        RlimitResource::Core => libc::RLIMIT_CORE,
        RlimitResource::Data => libc::RLIMIT_DATA,
        RlimitResource::MemLock => libc::RLIMIT_MEMLOCK,
        RlimitResource::NoFile => libc::RLIMIT_NOFILE,
        RlimitResource::Stack => libc::RLIMIT_STACK,
    };

    let rlimit = libc::rlimit {
        rlim_cur: value,
        rlim_max: value,
    };

    let result = unsafe { libc::setrlimit(resource_id, &rlimit) };

    if result != 0 {
        return Err(MemcallError::SystemError(format!(
            "<memcall> could not set rlimit [Err: {}]",
            std::io::Error::last_os_error()
        )));
    }

    Ok(())
}
