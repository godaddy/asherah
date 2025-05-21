#!/bin/bash
set -e

echo "Building Asherah CLI example..."
cargo build --release

echo "Build complete. Example usage:"
echo ""
echo "# Run with default settings (in-memory metastore, static KMS):"
echo "cargo run --release -- --count 100 --sessions 5"
echo ""
echo "# Run with MySQL metastore:"
echo "cargo run --release -- --metastore mysql --mysql-url \"mysql://user:password@localhost:3306/database\""
echo ""
echo "# Run with AWS KMS:"
echo "cargo run --release -- --kms aws --region us-west-2 --key-id \"alias/your-key-alias-or-arn\""
echo ""
echo "# For more options, see:"
echo "cargo run --release -- --help"