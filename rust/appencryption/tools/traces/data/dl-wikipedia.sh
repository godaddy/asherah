#!/bin/bash
# Download Wikipedia trace files
set -e

# Create directory if it doesn't exist
mkdir -p data

echo "Downloading Wikipedia trace files..."

# Download from WikiBench
WIKI_URL="http://www.wikibench.eu/wiki"

# Download specific trace files (only a small sample for testing)
FILES=(
  "wiki-1190225316.gz"  # A small sample file
)

for file in "${FILES[@]}"; do
  if [ ! -f "data/$file" ]; then
    echo "Downloading $file..."
    curl -sL "$WIKI_URL/$file" -o "data/$file"
  else
    echo "$file already exists, skipping..."
  fi
done

echo "All Wikipedia trace files downloaded successfully!"