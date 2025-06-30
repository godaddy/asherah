use actix_web::{web, App, HttpResponse, HttpServer, Responder};
use appencryption::{
    kms::StaticKeyManagementService,
    metastore::InMemoryMetastore,
    policy::CryptoPolicy,
    session::{Session, SessionFactory},
};
use base64::prelude::*;
use securememory::protected_memory::DefaultSecretFactory;
use serde::{Deserialize, Serialize};
use std::sync::Arc;

/// This example demonstrates how to integrate Asherah with the Actix web framework.
///
/// It shows:
/// 1. Setting up Asherah as an application-wide service
/// 2. Creating sessions for user-specific encryption
/// 3. Integrating with a REST API for encrypting/decrypting data
/// 4. Handling asynchronous encryption/decryption in a web context
/// 5. Error handling and response formatting

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

// AppState to hold our Asherah SessionFactory
struct AppState {
    session_factory: Arc<SessionFactory>,
}

// API handlers
async fn encrypt(data: web::Json<EncryptRequest>, state: web::Data<AppState>) -> impl Responder {
    let user_id = &data.user_id;
    let plaintext = data.data.as_bytes();

    // Create a session for this user
    match state.session_factory.session(user_id).await {
        Ok(session) => {
            // Encrypt the data
            match session.encrypt(plaintext).await {
                Ok(encrypted) => {
                    // Encode as base64 for JSON transport
                    let encrypted_b64 = BASE64_STANDARD.encode(&encrypted.data);
                    HttpResponse::Ok().json(EncryptResponse {
                        encrypted_data: encrypted_b64,
                        key_id: encrypted.key.id.clone(),
                    })
                }
                Err(e) => {
                    HttpResponse::InternalServerError().body(format!("Encryption error: {}", e))
                }
            }
        }
        Err(e) => HttpResponse::InternalServerError().body(format!("Session error: {}", e)),
    }
}

async fn decrypt(data: web::Json<DecryptRequest>, state: web::Data<AppState>) -> impl Responder {
    let user_id = &data.user_id;

    // Decode base64 encrypted data
    let encrypted_data = match BASE64_STANDARD.decode(&data.encrypted_data) {
        Ok(data) => data,
        Err(e) => {
            return HttpResponse::BadRequest().body(format!("Invalid base64 data: {}", e));
        }
    };

    // Create a DataRowRecord for decryption
    use appencryption::envelope::{DataRowRecord, EnvelopeKeyRecord};
    let key_record = EnvelopeKeyRecord {
        revoked: None,
        id: data.key_id.clone(),
        created: 0,            // We don't have the actual created time from the request
        encrypted_key: vec![], // We don't need to provide the encrypted key for decryption
        parent_key_meta: None,
    };
    let record = DataRowRecord {
        data: encrypted_data,
        key: key_record,
    };

    // Create a session for this user
    match state.session_factory.session(user_id).await {
        Ok(session) => {
            // Decrypt the data
            match session.decrypt(&record).await {
                Ok(decrypted) => {
                    // Convert to UTF-8 string
                    match String::from_utf8(decrypted) {
                        Ok(text) => HttpResponse::Ok().json(DecryptResponse { data: text }),
                        Err(e) => {
                            HttpResponse::BadRequest().body(format!("Invalid UTF-8 data: {}", e))
                        }
                    }
                }
                Err(e) => {
                    HttpResponse::InternalServerError().body(format!("Decryption error: {}", e))
                }
            }
        }
        Err(e) => HttpResponse::InternalServerError().body(format!("Session error: {}", e)),
    }
}

async fn health() -> impl Responder {
    HttpResponse::Ok().body("Service is healthy")
}

#[actix_web::main]
async fn main() -> std::io::Result<()> {
    println!("Actix Web Integration Example");
    println!("============================");

    // Initialize Asherah dependencies
    println!("Initializing Asherah...");

    // Create crypto policy
    use chrono::TimeDelta;
    let expire_after = TimeDelta::hours(24);
    let cache_max_age = TimeDelta::hours(2);
    let create_date_precision = TimeDelta::minutes(1);

    let policy = CryptoPolicy::new()
        .with_expire_after(expire_after.to_std().unwrap())
        .with_session_cache()
        .with_session_cache_duration(cache_max_age.to_std().unwrap())
        .with_create_date_precision(create_date_precision.to_std().unwrap());

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
    let app_state = web::Data::new(AppState {
        session_factory: factory,
    });

    println!("Starting Actix Web server on http://127.0.0.1:8080");
    println!("Available endpoints:");
    println!("  GET  /health");
    println!("  POST /encrypt");
    println!("  POST /decrypt");

    // Start Actix HTTP server
    HttpServer::new(move || {
        App::new()
            .app_data(app_state.clone())
            .route("/health", web::get().to(health))
            .route("/encrypt", web::post().to(encrypt))
            .route("/decrypt", web::post().to(decrypt))
    })
    .bind("127.0.0.1:8080")?
    .run()
    .await
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
