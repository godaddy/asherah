//! # memcall
//!
//! Cross-platform wrapper for memory-related system calls.
//!
//! This library provides a platform-independent interface for memory-related operations:
//! - Memory allocation/deallocation
//! - Memory protection management
//! - Memory locking to prevent swapping to disk
//!
//! The implementation uses the appropriate system calls for each supported platform.

mod error;
mod types;

#[cfg(target_os = "windows")]
pub(crate) mod windows;
#[cfg(target_os = "windows")]
use windows as platform;

#[cfg(target_os = "linux")]
pub(crate) mod unix;
#[cfg(target_os = "linux")]
use unix as platform;

#[cfg(target_os = "macos")]
pub(crate) mod unix;
#[cfg(target_os = "macos")]
use unix as platform;

#[cfg(target_os = "freebsd")]
pub(crate) mod freebsd;
#[cfg(target_os = "freebsd")]
use freebsd as platform;

#[cfg(target_os = "netbsd")]
pub(crate) mod netbsd;
#[cfg(target_os = "netbsd")]
use netbsd as platform;

#[cfg(target_os = "openbsd")]
pub(crate) mod openbsd;
#[cfg(target_os = "openbsd")]
use openbsd as platform;

#[cfg(target_os = "solaris")]
pub(crate) mod solaris;
#[cfg(target_os = "solaris")]
use solaris as platform;

#[cfg(target_os = "aix")]
pub(crate) mod aix;
#[cfg(target_os = "aix")]
use aix as platform;

// Error type
pub use error::MemcallError;
// Types
pub use types::{MemoryProtection, RlimitResource};

// Platform-agnostic API

/// Allocates a new memory region with the given size and accessibility.
pub fn alloc(size: usize) -> Result<&'static mut [u8], MemcallError> {
    platform::alloc(size)
}

/// Frees a memory region previously allocated with `alloc`.
pub fn free(ptr: &mut [u8]) -> Result<(), MemcallError> {
    platform::free(ptr)
}

/// Protects a memory region with the specified protection flags.
pub fn protect(ptr: &mut [u8], protection: MemoryProtection) -> Result<(), MemcallError> {
    platform::protect(ptr, protection)
}

/// Locks a memory region to prevent it from being swapped to disk.
pub fn lock(ptr: &mut [u8]) -> Result<(), MemcallError> {
    platform::lock(ptr)
}

/// Unlocks a memory region previously locked with `lock`.
pub fn unlock(ptr: &mut [u8]) -> Result<(), MemcallError> {
    platform::unlock(ptr)
}

/// Returns the system's page size.
pub fn page_size() -> usize {
    platform::page_size()
}

/// Disables creation of core dump files for the current process.
pub fn disable_core_dumps() -> Result<(), MemcallError> {
    platform::disable_core_dumps()
}

/// Sets a resource limit for the current process.
pub fn set_limit(resource: RlimitResource, value: u64) -> Result<(), MemcallError> {
    platform::set_limit(resource, value)
}

/// Allocates aligned memory to the specified size and alignment, ensuring it's page-aligned.
///
/// This is a Rust implementation that matches the functionality expected by memguard.
///
/// # Arguments
///
/// * `size` - The size of memory to allocate in bytes
/// * `alignment` - The byte alignment for the allocation (typically page_size)
///
/// # Returns
///
/// * `Result<*mut u8, MemcallError>` - A raw pointer to the allocated memory or an error
///
/// # Safety
///
/// - The returned pointer must only be used according to the contract of this function
/// - The caller should eventually call free_aligned to release the memory
pub fn allocate_aligned(size: usize, _alignment: usize) -> Result<*mut u8, MemcallError> {
    // We ignore the alignment parameter since alloc already ensures page alignment
    let buf = alloc(size)?;
    // Return the pointer to the allocated memory
    Ok(buf.as_mut_ptr())
}

/// Frees memory previously allocated with allocate_aligned.
///
/// # Arguments
///
/// * `ptr` - Pointer to the memory region to free
/// * `size` - Size of the memory region
///
/// # Safety
///
/// - The pointer must have been returned by allocate_aligned
/// - No other references to the memory should exist after this call
pub unsafe fn free_aligned(ptr: *mut u8, size: usize) -> Result<(), MemcallError> {
    let slice = std::slice::from_raw_parts_mut(ptr, size);
    free(slice)
}
