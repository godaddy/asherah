// Integration test module organization

pub mod common;
pub mod memory;
pub mod dynamodb;
pub mod mysql;
pub mod postgres;
pub mod multithreaded;

// Additional tests
mod metastore_interactions_test;
mod parameterized_test;
mod cache_behavior_test;