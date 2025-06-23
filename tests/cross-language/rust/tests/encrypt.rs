use anyhow::Result;
use cucumber::{given, then, when, World};
use cltf::{constants::*, TestContext};
use std::{fs, path::Path};

#[derive(World, Debug, Default)]
pub struct EncryptWorld {
    ctx: TestContext,
}

#[given(expr = "I have {string}")]
fn i_have(world: &mut EncryptWorld, payload: String) {
    world.ctx.payload_string = Some(payload);
}

#[when("I encrypt the data")]
async fn i_encrypt_the_data(world: &mut EncryptWorld) -> Result<()> {
    world.ctx.connect_sql()?;
    world.ctx.encrypt_data()?;
    Ok(())
}

#[then("I should get encrypted_data")]
fn i_should_get_encrypted_data(world: &mut EncryptWorld) -> Result<()> {
    let file_path = format!("{}{}", FILE_DIRECTORY, FILE_NAME);
    
    // Remove file if it exists
    if Path::new(&file_path).exists() {
        fs::remove_file(&file_path)?;
    }
    
    // Write encrypted data to file
    fs::write(
        &file_path,
        world.ctx.encrypted_payload.as_ref().expect("Encrypted payload should exist"),
    )?;
    
    Ok(())
}

#[then("encrypted_data should not be equal to data")]
fn encrypted_data_should_not_be_equal_to_data(world: &mut EncryptWorld) -> Result<()> {
    let payload = world.ctx.payload_string.as_ref().expect("Payload should exist");
    let encrypted = world.ctx.encrypted_payload.as_ref().expect("Encrypted payload should exist");
    
    assert_ne!(payload, encrypted, "Encryption failed, payload equals encrypted data");
    Ok(())
}

#[tokio::main]
async fn main() {
    // Initialize tracing for better test output
    tracing_subscriber::fmt()
        .with_env_filter("info")
        .init();
        
    EncryptWorld::cucumber()
        .run_and_exit("../features/encrypt.feature")
        .await;
}