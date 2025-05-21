use crate::error::MemcallError;
use crate::types::{MemoryProtection, RlimitResource};
use libc;
use once_cell::sync::Lazy;
use std::ptr;

static PAGE_SIZE: Lazy<usize> = Lazy::new(|| unsafe { libc::sysconf(libc::_SC_PAGESIZE) as usize });

#[inline]
fn as_mut_ptr(memory: &mut [u8]) -> *mut libc::c_void {
    memory.as_mut_ptr() as *mut libc::c_void
}

#[inline]
fn as_len(memory: &[u8]) -> libc::size_t {
    memory.len() as libc::size_t
}

// Error message for invalid flag, matching Go's ErrInvalidFlag
const ERR_INVALID_FLAG: &str = "<memcall> memory protection flag is undefined";

pub fn alloc(size: usize) -> Result<&'static mut [u8], MemcallError> {
    let ptr = unsafe {
        libc::mmap(
            ptr::null_mut(),
            size,
            libc::PROT_READ | libc::PROT_WRITE,
            libc::MAP_PRIVATE | libc::MAP_ANON | libc::MAP_CONCEAL,
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

    let memory = unsafe { std::slice::from_raw_parts_mut(ptr as *mut u8, size) };
    for byte in memory.iter_mut() {
        *byte = 0;
    }
    Ok(memory)
}

pub fn free(ptr: &mut [u8]) -> Result<(), MemcallError> {
    if ptr.is_empty() {
        return Ok(());
    }

    if let Err(err_protect) = protect(ptr, MemoryProtection::ReadWrite) {
        return Err(err_protect);
    }

    for byte in ptr.iter_mut() {
        *byte = 0;
    }

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

pub fn protect(ptr: &mut [u8], protection: MemoryProtection) -> Result<(), MemcallError> {
    if ptr.is_empty() {
        return Ok(());
    }

    let prot = match protection as u32 {
        1 => libc::PROT_NONE,
        2 => libc::PROT_READ,
        6 => libc::PROT_READ | libc::PROT_WRITE,
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

pub fn lock(ptr: &mut [u8]) -> Result<(), MemcallError> {
    if ptr.is_empty() {
        return Ok(());
    }
    // OpenBSD Go implementation does not call madvise here.
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

pub fn unlock(ptr: &mut [u8]) -> Result<(), MemcallError> {
    if ptr.is_empty() {
        return Ok(());
    }

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

pub fn page_size() -> usize {
    *PAGE_SIZE
}

pub fn disable_core_dumps() -> Result<(), MemcallError> {
    let rlimit = libc::rlimit {
        rlim_cur: 0,
        rlim_max: 0,
    };
    let result = unsafe { libc::setrlimit(libc::RLIMIT_CORE, &rlimit) };
    if result != 0 {
        return Err(MemcallError::SystemError(format!(
            "<memcall> could not set rlimit [Err: {}]",
            std::io::Error::last_os_error()
        )));
    }
    Ok(())
}

pub fn set_limit(resource: RlimitResource, value: u64) -> Result<(), MemcallError> {
    let resource_id = match resource {
        RlimitResource::Core => libc::RLIMIT_CORE,
        RlimitResource::Data => libc::RLIMIT_DATA,
        RlimitResource::MemLock => libc::RLIMIT_MEMLOCK,
        RlimitResource::NoFile => libc::RLIMIT_NOFILE,
        RlimitResource::Stack => libc::RLIMIT_STACK,
    };

    let rlimit = libc::rlimit {
        rlim_cur: value as libc::rlim_t,
        rlim_max: value as libc::rlim_t,
    };

    let result = unsafe { libc::setrlimit(resource_id, &rlimit) };
    if result != 0 {
        return Err(MemcallError::SystemError(format!(
            "<memcall> could not set rlimit for resource {} [Err: {}]",
            resource_id,
            std::io::Error::last_os_error()
        )));
    }
    Ok(())
}
