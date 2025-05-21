#!/bin/bash
# Download Cache2k trace files
set -e

# Create directory if it doesn't exist
mkdir -p data

echo "Downloading Cache2k trace files..."

# Download from cache2k-benchmark GitHub repository
CACHE2K_URL="https://github.com/cache2k/cache2k-benchmark/raw/master/traces"

# Download specific trace files
FILES=(
  "trace-glimpse.trc.bin.gz"
  "trace-multi2.trc.bin.gz"
  "trace-oltp.trc.bin.gz"
  "trace-sprite.trc.bin.gz"
  "trace-mt-db-1-busy.trc.bin.bz2"
  "trace-mt-db-1-night.trc.bin.bz2"
)

for file in "${FILES[@]}"; do
  if [ ! -f "data/$file" ]; then
    echo "Downloading $file..."
    curl -sL "$CACHE2K_URL/$file" -o "data/$file"
  else
    echo "$file already exists, skipping..."
  fi
done

echo "All Cache2k trace files downloaded successfully!"