//! Tests for the AWS v2 KMS implementation
//!
//! These tests validate the AWS KMS implementation using mocks.

// Import the test modules
pub mod aws_kms_builder_unit_test;
pub mod builder_test;
pub mod kms_test;

// Import dependencies from parent modules
use super::*;
use crate::crypto::Aes256GcmAead;
use crate::error::{Error, Result};
use crate::KeyManagementService;
use async_trait::async_trait;
use std::collections::HashMap;
use std::sync::Arc;