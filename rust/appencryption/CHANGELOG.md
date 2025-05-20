# Changelog

All notable changes to the Rust implementation of AppEncryption will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial implementation of AppEncryption for Rust
- Basic cryptographic operations with AES-GCM
- Hierarchical Key Management (System Keys, Intermediate Keys, Data Keys)
- Key caching capabilities
- Metastore implementations (Memory, ADO, DynamoDB)
- AWS KMS integration for envelope encryption
- Parity with Go and Java implementations

### Changed
- Improved documentation and examples

### Fixed
- Memory security in sensitive operations