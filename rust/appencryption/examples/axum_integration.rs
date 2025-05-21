#![allow(clippy::unwrap_used)]
#![feature(rustc_attrs)]
#![rustc_edition = "2021"]

use appencryption::{
    envelope::{DataRowRecord, EnvelopeKeyRecord},
    kms::StaticKeyManagementService,
    metastore::InMemoryMetastore,
    policy::CryptoPolicy,
    session::{Session, SessionFactory},
};
use axum::{
    extract::State,
    routing::{get, post},
    Json, Router,
};
use color_eyre::eyre::Result;
use log::{debug, error, info};
use securememory::protected_memory::DefaultSecretFactory;
use serde::{Deserialize, Serialize};
use std::{net::SocketAddr, sync::Arc, time::Duration};
use tower::ServiceBuilder;
use tower_http::trace::TraceLayer;

/// This example demonstrates how to integrate Asherah with the Axum web framework.
///
/// It shows:
/// 1. Setting up Asherah as an application-wide service
/// 2. Creating sessions for user-specific encryption
/// 3. Integrating with a REST API for encrypting/decrypting data
/// 4. Handling asynchronous encryption/decryption in a web context
/// 5. Error handling and response formatting
/// 6. Proper logging setup and request tracing

// Data types for our API
#[derive(Deserialize)]
struct EncryptRequest {
    user_id: String,
    data: String,
}

#[derive(Serialize)]
struct EncryptResponse {
    encrypted_data: String,
    key_id: String,
}

#[derive(Deserialize)]
struct DecryptRequest {
    user_id: String,
    encrypted_data: String,
    key_id: String,
}

#[derive(Serialize)]
struct DecryptResponse {
    data: String,
}

// Error response structure
#[derive(Serialize)]
struct ErrorResponse {
    error: String,
}

// AppState to hold our Asherah SessionFactory
#[derive(Clone)]
struct AppState {
    session_factory: Arc<SessionFactory>,
}

// API handlers
async fn health() -> &'static str {
    "Service is healthy"
}

async fn encrypt(
    State(state): State<AppState>,
    Json(payload): Json<EncryptRequest>,
) -> Result<Json<EncryptResponse>, Json<ErrorResponse>> {
    debug!("Processing encrypt request for user {}", payload.user_id);
    let user_id = &payload.user_id;
    let plaintext = payload.data.as_bytes();

    // Create a session for this user
    let session = state.session_factory.session(user_id).await.map_err(|e| {
        error!("Failed to create session: {}", e);
        Json(ErrorResponse {
            error: format!("Session error: {}", e),
        })
    })?;

    // Encrypt the data
    let encrypted = session.encrypt(plaintext).await.map_err(|e| {
        error!("Encryption failed: {}", e);
        Json(ErrorResponse {
            error: format!("Encryption error: {}", e),
        })
    })?;

    // Encode as base64 for JSON transport
    let encrypted_b64 = base64::encode(&encrypted.data);
    debug!("Successfully encrypted data for user {}", user_id);

    Ok(Json(EncryptResponse {
        encrypted_data: encrypted_b64,
        key_id: encrypted.key.id.clone(),
    }))
}

async fn decrypt(
    State(state): State<AppState>,
    Json(payload): Json<DecryptRequest>,
) -> Result<Json<DecryptResponse>, Json<ErrorResponse>> {
    debug!("Processing decrypt request for user {}", payload.user_id);
    let user_id = &payload.user_id;

    // Decode base64 encrypted data
    let encrypted_data = base64::decode(&payload.encrypted_data).map_err(|e| {
        error!("Invalid base64 data: {}", e);
        Json(ErrorResponse {
            error: format!("Invalid base64 data: {}", e),
        })
    })?;

    // Create a DataRowRecord for decryption
    let key_record = EnvelopeKeyRecord {
        revoked: None,
        id: payload.key_id.clone(),
        created: 0,            // We don't have the actual created time from the request
        encrypted_key: vec![], // We don't need to provide the encrypted key for decryption
        parent_key_meta: None,
    };

    let record = DataRowRecord {
        data: encrypted_data,
        key: key_record,
    };

    // Create a session for this user
    let session = state.session_factory.session(user_id).await.map_err(|e| {
        error!("Failed to create session: {}", e);
        Json(ErrorResponse {
            error: format!("Session error: {}", e),
        })
    })?;

    // Decrypt the data
    let decrypted = session.decrypt(&record).await.map_err(|e| {
        error!("Decryption failed: {}", e);
        Json(ErrorResponse {
            error: format!("Decryption error: {}", e),
        })
    })?;

    // Convert to UTF-8 string
    let text = String::from_utf8(decrypted).map_err(|e| {
        error!("Invalid UTF-8 data: {}", e);
        Json(ErrorResponse {
            error: format!("Invalid UTF-8 data: {}", e),
        })
    })?;

    debug!("Successfully decrypted data for user {}", user_id);
    Ok(Json(DecryptResponse { data: text }))
}

#[tokio::main]
async fn main() -> Result<()> {
    // Initialize better error handling
    color_eyre::install()?;

    // Initialize env_logger with default configuration
    env_logger::init();

    info!("Axum Integration Example");
    info!("=======================");

    // Initialize Asherah dependencies
    info!("Initializing Asherah...");

    // Create crypto policy with clear duration comments
    let policy = CryptoPolicy::new()
        .with_expire_after(Duration::from_secs(60 * 60 * 24)) // 24 hours
        .with_session_cache()
        .with_session_cache_duration(Duration::from_secs(60 * 60 * 2)) // 2 hours
        .with_create_date_precision(Duration::from_secs(60)); // 1 minute

    let master_key = vec![0_u8; 32]; // In a real app, use a secure key
    let kms = Arc::new(StaticKeyManagementService::new(master_key));
    let metastore = Arc::new(InMemoryMetastore::new());
    let secret_factory = Arc::new(DefaultSecretFactory::new());

    // Create session factory
    let factory = Arc::new(SessionFactory::new(
        "service",
        "product",
        policy,
        kms,
        metastore,
        secret_factory,
        vec![],
    ));

    // Create app state
    let app_state = AppState {
        session_factory: factory,
    };

    // Build application
    let app = Router::new()
        .route("/health", get(health))
        .route("/encrypt", post(encrypt))
        .route("/decrypt", post(decrypt))
        .with_state(app_state)
        .layer(
            ServiceBuilder::new()
                .layer(TraceLayer::new_for_http())
                .into_inner(),
        );

    // Get the address to bind to
    let addr = SocketAddr::from(([127, 0, 0, 1], 8080));
    info!("Starting Axum server on http://{}", addr);
    info!("Available endpoints:");
    info!("  GET  /health");
    info!("  POST /encrypt");
    info!("  POST /decrypt");

    // Start the server
    axum::Server::bind(&addr)
        .serve(app.into_make_service())
        .await
        .map_err(|e| color_eyre::eyre::eyre!("Server error: {}", e))?;

    Ok(())
}

// Example curl commands to test:
//
// Encrypt:
// curl -X POST http://localhost:8080/encrypt \
//   -H "Content-Type: application/json" \
//   -d '{"user_id": "user123", "data": "This is secret data"}'
//
// Decrypt (use the encrypted_data and key_id from encrypt response):
// curl -X POST http://localhost:8080/decrypt \
//   -H "Content-Type: application/json" \
//   -d '{"user_id": "user123", "encrypted_data": "...", "key_id": "..."}'
