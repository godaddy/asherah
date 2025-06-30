use anyhow::Result;
use cucumber::{given, then, when, World};
use cltf::TestContext;
use std::fs;

#[derive(World, Debug, Default)]
pub struct DecryptWorld {
    ctx: TestContext,
}

#[given(expr = "I have encrypted_data from {string}")]
fn i_have_encrypted_data_from(world: &mut DecryptWorld, file_name: String) -> Result<()> {
    let file_path = format!("/tmp/{}", file_name);
    let data = fs::read_to_string(file_path)?;
    world.ctx.encrypted_payload = Some(data);
    Ok(())
}

#[when("I decrypt the encrypted_data")]
async fn i_decrypt_the_encrypted_data(world: &mut DecryptWorld) -> Result<()> {
    world.ctx.connect_sql()?;
    world.ctx.decrypt_data()?;
    Ok(())
}

#[then("I should get decrypted_data")]
fn i_should_get_decrypted_data(world: &mut DecryptWorld) -> Result<()> {
    assert!(world.ctx.decrypted_payload.is_some(), "Decryption failed");
    Ok(())
}

#[then(expr = "decrypted_data should be equal to {string}")]
fn decrypted_data_should_be_equal_to(world: &mut DecryptWorld, expected: String) -> Result<()> {
    let decrypted = world.ctx.decrypted_payload.as_ref().expect("Decrypted payload should exist");
    assert_eq!(decrypted, &expected, "Decrypted payload does not match expected value");
    Ok(())
}

#[tokio::main]
async fn main() {
    // Initialize tracing for better test output
    tracing_subscriber::fmt()
        .with_env_filter("info")
        .init();
        
    DecryptWorld::cucumber()
        .run_and_exit("../features/decrypt.feature")
        .await;
}