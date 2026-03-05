# Large Payload Test Tool

Tests Asherah Go SDK encryption/decryption with large payloads (25-100MB+). Performs a full round-trip for each payload size: generate random data, SHA-256 checksum, encrypt, save encrypted DRR to file, validate ciphertext integrity, load from file, decrypt, and verify the checksum matches.

Exits with code 0 if all sizes pass, 1 if any fail.

## Local Usage

```bash
cd samples/go/largepayload
go build -o largepayload .

# Default sizes (25, 50, 100 MB)
./largepayload

# Custom sizes
./largepayload -sizes 1,10,25

# Keep output files for inspection
./largepayload -sizes 25 -outdir ./output -keep
```

## Docker Usage

Build from the repository root:

```bash
docker build -f samples/go/largepayload/Dockerfile -t asherah-largepayload .
docker run --rm asherah-largepayload
docker run --rm asherah-largepayload -sizes 1,25,50
```

## Flags

| Flag | Default | Description |
|------|---------|-------------|
| `-sizes` | `25,50,100` | Comma-separated payload sizes in MB |
| `-outdir` | (temp dir) | Output directory for DRR JSON files |
| `-keep` | `false` | Retain output files after test completes |

## What It Tests

For each payload size:

1. Generates cryptographically random data
2. Computes a SHA-256 checksum of the plaintext
3. Encrypts using Asherah with in-memory metastore and static KMS
4. Validates the ciphertext is non-empty and not all zeros
5. Writes the `DataRowRecord` to a JSON file on disk
6. Reads the file back and decrypts
7. Verifies the decrypted data matches the original checksum
8. Reports encrypt/decrypt timing and memory usage
