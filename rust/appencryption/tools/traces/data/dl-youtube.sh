#!/bin/bash
# Download YouTube trace files
set -e

# Create directory if it doesn't exist
mkdir -p data

echo "Downloading YouTube trace files..."

# Download from UMASS Trace Repository
YOUTUBE_URL="http://traces.cs.umass.edu/index.php/Network/Network"

# Note: Direct downloading from UMASS requires authentication
# For demonstration purposes, we'll provide instructions instead
echo "YouTube traces require manual download from:"
echo "$YOUTUBE_URL"
echo ""
echo "After downloading, place the files in the data/ directory"
echo "The typical filename pattern is: youtube-*.gz"
echo ""
echo "Alternatively, you can use the zipf.rs module to generate synthetic workloads"