# TODO Items for Next Version

## Database Support
- Complete Oracle database implementation when a suitable Rust driver becomes available
- ✅ Implement ADO (ActiveX Data Objects) metastore to provide generic database connectivity similar to C# (placeholder implementation done, actual implementation pending suitable driver)

## Cache Implementations
- ✅ Finalize SLRU (Segmented LRU) cache implementation referenced in policy settings (implementation already complete)
- ✅ Clarify Simple cache implementation details in documentation (implementation already complete with documentation)

## SecureMemory Improvements
- ✅ Improve test isolation to prevent global state conflicts between tests (implemented TestGuard and isolation utilities)
- ✅ Document improvement plan for remaining SecureMemory issues (created IMPROVEMENT_PLAN.md)
- Enhance Enclave component with better memory handling to address access violations
- Fix remaining SIGBUS errors in memory access operations
- Review global state management to eliminate deadlocks

## AWS Integration
- ✅ Complete the custom KMS client factory implementation in AWS v2 plugin (implementation already complete)
- ✅ Finish builder pattern implementations in AWS v2 plugin (implementation already complete)
- ✅ Add unit tests for builder pattern classes (added comprehensive unit tests for AWS KMS builder)

## Documentation and Testing
- ✅ Complete SQL Server metastore implementation documentation (created SQL_SERVER_METASTORE.md)
- ✅ Create comprehensive examples for different metastore implementations (added sqlserver_example.rs)
- ✅ Add migration guides for users moving from other language implementations (created MIGRATION_GUIDE.md)
- ✅ Ensure full compatibility testing with Go, Java, and C# implementations (added cross-language compatibility tests)

## Performance Optimization
- ✅ Create performance optimization plan (created PERFORMANCE_OPTIMIZATION.md with detailed strategy)
- Continue optimizing cache implementations
- Implement zero-copy operations where possible
- Explore using const generics for performance improvements