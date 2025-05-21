//! Tests for the AWS v2 DynamoDB metastore implementation
//!
//! These tests validate the DynamoDB metastore implementation using mocks.

// Import the test modules
pub mod dynamodb_test;

// Import dependencies from parent modules
use super::*;
use crate::envelope::{EnvelopeKeyRecord, KeyMeta};
use crate::Metastore;
use crate::error::Result;
use async_trait::async_trait;
use std::collections::HashMap;
use std::sync::{Arc, Mutex};
use std::time::Duration;