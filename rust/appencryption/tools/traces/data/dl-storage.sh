#!/bin/bash
# Download Storage trace files
set -e

# Create directory if it doesn't exist
mkdir -p data

echo "Downloading Storage trace files..."

# Download from UMASS Trace Repository
STORAGE_URL="http://traces.cs.umass.edu/index.php/Storage/Storage"

# Note: Direct downloading from UMASS requires authentication
# For demonstration purposes, we'll provide instructions instead
echo "Storage traces require manual download from:"
echo "$STORAGE_URL"
echo ""
echo "After downloading, place the files in the data/ directory"
echo "The typical filename pattern is: WebSearch*.spc.gz"
echo ""
echo "Alternatively, you can use the zipf.rs module to generate synthetic workloads"