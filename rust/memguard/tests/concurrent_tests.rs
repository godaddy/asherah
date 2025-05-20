//! Tests that can safely run concurrently
//! These tests DO NOT test lifecycle management (init/destroy) 
//! of the global state.

// Include all the normal tests here that don't destroy global state
// This test file will be run normally with cargo test and will run with concurrency