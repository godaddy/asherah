// Integration test module organization

pub mod common;
pub mod dynamodb;
pub mod memory;
pub mod multithreaded;
pub mod mysql;
pub mod postgres;

// Additional tests
mod cache_behavior_test;
mod metastore_interactions_test;
mod parameterized_test;
