# Changelog

All notable changes to the Rust implementation of MemGuard will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Comprehensive concurrent test suite
- Improved lifecycle tests with proper serialization

### Changed
- Fixed test warnings and code organization
- Changed lifecycle tests from ignored to serialized execution
- Implemented functional tests for purge, buffer, enclave, and stream lifecycle operations