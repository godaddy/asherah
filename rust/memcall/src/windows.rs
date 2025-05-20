use crate::error::MemcallError;
use crate::types::{MemoryProtection, RlimitResource};
use std::ptr;
use windows_sys::Win32::System::Memory::{
    VirtualAlloc, VirtualFree, VirtualLock, VirtualProtect, VirtualUnlock, MEM_COMMIT, MEM_RELEASE,
    MEM_RESERVE, PAGE_NOACCESS, PAGE_READONLY, PAGE_READWRITE,
};
use windows_sys::Win32::System::SystemInformation::{GetSystemInfo, SYSTEM_INFO};

// Helper to get a pointer for Windows API calls
#[inline]
fn as_ptr_void(memory: &mut [u8]) -> *mut std::ffi::c_void {
    if memory.is_empty() {
        // Match Go's _zero behavior for functions like VirtualAlloc if needed,
        // but for most operations on an existing slice, as_mut_ptr is fine.
        // For VirtualAlloc, we pass ptr::null_mut() directly.
        // For operations on existing slices, an empty slice check is done first.
        ptr::null_mut()
    } else {
        memory.as_mut_ptr() as *mut std::ffi::c_void
    }
}

#[inline]
fn as_len_usize(memory: &[u8]) -> usize {
    memory.len()
}

// Error message for invalid flag, matching Go's ErrInvalidFlag
const ERR_INVALID_FLAG: &str = "<memcall> memory protection flag is undefined";

pub fn alloc(size: usize) -> Result<&'static mut [u8], MemcallError> {
    let ptr = unsafe {
        VirtualAlloc(
            ptr::null_mut(),
            size,
            MEM_COMMIT | MEM_RESERVE,
            PAGE_READWRITE,
        )
    };

    if ptr.is_null() {
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

    let result = unsafe { VirtualFree(as_ptr_void(ptr), 0, MEM_RELEASE) };
    if result == 0 {
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

    let prot = match protection {
        MemoryProtection::NoAccess => PAGE_NOACCESS,
        MemoryProtection::ReadOnly => PAGE_READONLY,
        MemoryProtection::ReadWrite => PAGE_READWRITE,
        _ => return Err(MemcallError::InvalidArgument(ERR_INVALID_FLAG.to_string())),
    };

    let mut old_protect: u32 = 0;
    let result =
        unsafe { VirtualProtect(as_ptr_void(ptr), as_len_usize(ptr), prot, &mut old_protect) };

    if result == 0 {
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
    let result = unsafe { VirtualLock(as_ptr_void(ptr), as_len_usize(ptr)) };
    if result == 0 {
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
    let result = unsafe { VirtualUnlock(as_ptr_void(ptr), as_len_usize(ptr)) };
    if result == 0 {
        return Err(MemcallError::SystemError(format!(
            "<memcall> could not free lock on {:p} [Err: {}]",
            ptr.as_ptr(),
            std::io::Error::last_os_error()
        )));
    }
    Ok(())
}

pub fn page_size() -> usize {
    unsafe {
        let mut si: SYSTEM_INFO = std::mem::zeroed();
        GetSystemInfo(&mut si);
        si.dwPageSize as usize
    }
}

pub fn disable_core_dumps() -> Result<(), MemcallError> {
    Ok(()) // No-op on Windows
}

pub fn set_limit(_resource: RlimitResource, _value: u64) -> Result<(), MemcallError> {
    Err(MemcallError::NotSupported(
        "set_limit is not supported on Windows".to_string(),
    ))
}
