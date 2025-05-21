/// Memory protection flags.
/// Values are explicitly set to match Go's implementation:
/// - NoAccess = 1
/// - ReadOnly = 2
/// - ReadWrite = 6 (PROT_READ | PROT_WRITE)
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
#[repr(u32)]
pub enum MemoryProtection {
    /// No access: memory cannot be read, written, or executed.
    NoAccess = 1,

    /// Read-only: memory can be read but not written or executed.
    ReadOnly = 2,

    /// Read-write: memory can be read and written but not executed.
    ReadWrite = 6,
}

// Implement conversion from MemoryProtection to u32
impl From<MemoryProtection> for u32 {
    fn from(prot: MemoryProtection) -> u32 {
        prot as u32
    }
}

/// Resource limit identifiers used with `set_limit`.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum RlimitResource {
    /// Maximum size of the process's data segment (heap).
    Data,

    /// Maximum size of a core file.
    Core,

    /// Maximum size that may be locked into memory.
    MemLock,

    /// Maximum number of open files.
    NoFile,

    /// Maximum size of the process's stack segment.
    Stack,
}
