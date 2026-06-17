#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

cd "$PROJECT_DIR"

echo "=== Building Asherah Native App JAR ==="
echo "Project directory: $PROJECT_DIR"
echo ""

# Build JAR with dependencies
mvn package -Pjar -DskipTests

echo ""
echo "=== Build Complete ==="
echo "JAR: $PROJECT_DIR/target/native-app-1.0.0-SNAPSHOT-jar-with-dependencies.jar"
echo ""
echo "Run with: java -jar target/native-app-1.0.0-SNAPSHOT-jar-with-dependencies.jar"

