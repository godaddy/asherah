-- SQL Server schema for Asherah encryption_key table
-- This table stores encrypted envelope keys

CREATE TABLE encryption_key (
    id NVARCHAR(255) NOT NULL,
    created DATETIME2 NOT NULL,
    key_record NVARCHAR(MAX) NOT NULL,
    PRIMARY KEY (id, created)
);

-- Optional: Add index for better performance on load_latest queries
CREATE INDEX idx_encryption_key_id_created 
ON encryption_key(id, created DESC);

-- Optional: If using specific schema
-- CREATE SCHEMA asherah;
-- CREATE TABLE asherah.encryption_key (...);

-- Notes:
-- 1. NVARCHAR(MAX) is used for key_record to store JSON data
-- 2. DATETIME2 provides better precision than DATETIME
-- 3. Primary key on (id, created) ensures uniqueness of key versions
-- 4. Index on (id, created DESC) optimizes "load latest" queries