#!/bin/bash
set -e

OUTDIR="out"
mkdir -p "$OUTDIR"

report() {
    NAME="$1"
    echo "=== Running test for $NAME ==="
    
    # Run the test with cargo
    RUSTFLAGS="--cfg test_traces" cargo test --release --package traces --lib -- --nocapture "$NAME" 2>&1 | tee "$OUTDIR/$NAME.txt"
    
    # Convert to lowercase for consistency
    LOWERCASE_NAME=$(echo "$NAME" | tr '[:upper:]' '[:lower:]')
    
    # Generate request-based visualization
    ./visualize-request.sh "$OUTDIR/request_$LOWERCASE_NAME"*.txt
    for OUTPUT in out.*; do
        mv -v "$OUTPUT" "$OUTDIR/$LOWERCASE_NAME-requests.${OUTPUT#*.}"
    done
    
    # Generate cache size visualization
    ./visualize-size.sh "$OUTDIR/size_$LOWERCASE_NAME"*.txt
    for OUTPUT in out.*; do
        mv -v "$OUTPUT" "$OUTDIR/$LOWERCASE_NAME-cachesize.${OUTPUT#*.}"
    done
    
    # Generate comparative visualizations between different metrics
    ./visualize-kms-vs-metastore.sh "$OUTDIR/request_$LOWERCASE_NAME"*.txt
    for OUTPUT in out.*; do
        mv -v "$OUTPUT" "$OUTDIR/$LOWERCASE_NAME-kms-vs-metastore.${OUTPUT#*.}"
    done
    
    # Generate latency distribution graph
    ./visualize-latency.sh "$OUTDIR/latency_$LOWERCASE_NAME"*.txt
    for OUTPUT in out.*; do
        mv -v "$OUTPUT" "$OUTDIR/$LOWERCASE_NAME-latency.${OUTPUT#*.}"
    done
    
    # Generate memory usage visualization if available
    if [ -f "$OUTDIR/memory_$LOWERCASE_NAME.txt" ]; then
        ./visualize-memory.sh "$OUTDIR/memory_$LOWERCASE_NAME.txt"
        for OUTPUT in out.*; do
            mv -v "$OUTPUT" "$OUTDIR/$LOWERCASE_NAME-memory.${OUTPUT#*.}"
        done
    fi
    
    echo "=== Completed test for $NAME ==="
}

# Create combined multi-visualization report
create_combined_report() {
    echo "=== Creating combined report ==="
    
    # Combine all cache size visualizations
    ./combine-png.sh "$OUTDIR/"*-cachesize.png
    mv combined.png "$OUTDIR/all-cachesize-comparison.png"
    
    # Combine all request rate visualizations
    ./combine-png.sh "$OUTDIR/"*-requests.png
    mv combined.png "$OUTDIR/all-requests-comparison.png"
    
    # Combine KMS vs Metastore visualizations
    ./combine-png.sh "$OUTDIR/"*-kms-vs-metastore.png
    mv combined.png "$OUTDIR/all-kms-vs-metastore-comparison.png"
    
    # Combine latency visualizations
    ./combine-png.sh "$OUTDIR/"*-latency.png
    mv combined.png "$OUTDIR/all-latency-comparison.png"
    
    # Create HTML report with all visualizations
    cat > "$OUTDIR/report.html" << EOF
<!DOCTYPE html>
<html>
<head>
    <title>Asherah Rust Performance Visualization</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        h1, h2 { color: #333; }
        .section { margin-bottom: 30px; }
        .image-container { display: flex; flex-wrap: wrap; gap: 20px; }
        .image-box { 
            border: 1px solid #ddd; 
            padding: 10px; 
            border-radius: 5px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }
        .image-box h3 { margin-top: 0; }
        table { border-collapse: collapse; width: 100%; }
        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
        th { background-color: #f2f2f2; }
        tr:nth-child(even) { background-color: #f9f9f9; }
    </style>
</head>
<body>
    <h1>Asherah Rust Performance Visualization</h1>
    <p>Report generated on $(date)</p>
    
    <div class="section">
        <h2>Cache Size Impact on Operation Rate</h2>
        <img src="all-cachesize-comparison.png" alt="Cache Size Comparison">
    </div>
    
    <div class="section">
        <h2>Request Count Impact on Operation Rate</h2>
        <img src="all-requests-comparison.png" alt="Requests Comparison">
    </div>
    
    <div class="section">
        <h2>KMS vs Metastore Operations</h2>
        <img src="all-kms-vs-metastore-comparison.png" alt="KMS vs Metastore Comparison">
    </div>
    
    <div class="section">
        <h2>Latency Distribution</h2>
        <img src="all-latency-comparison.png" alt="Latency Comparison">
    </div>
    
    <div class="section">
        <h2>Individual Trace Results</h2>
        <div class="image-container">
EOF

    # Add each trace's visualizations to the HTML
    for TRACE in $(ls "$OUTDIR/"*-requests.png | sed 's/.*\/\(.*\)-requests.png/\1/'); do
        cat >> "$OUTDIR/report.html" << EOF
            <div class="image-box">
                <h3>$TRACE</h3>
                <p>Request impact: <img src="$TRACE-requests.png" alt="$TRACE Requests" width="400"></p>
                <p>Cache size impact: <img src="$TRACE-cachesize.png" alt="$TRACE Cache Size" width="400"></p>
                <p>KMS vs Metastore: <img src="$TRACE-kms-vs-metastore.png" alt="$TRACE KMS vs Metastore" width="400"></p>
                <p>Latency: <img src="$TRACE-latency.png" alt="$TRACE Latency" width="400"></p>
            </div>
EOF
    done

    # Close the HTML
    cat >> "$OUTDIR/report.html" << EOF
        </div>
    </div>
</body>
</html>
EOF

    echo "=== Report created at $OUTDIR/report.html ==="
}

# Use first arg or default to a small subset of traces
TRACES="$@"
if [ -z "$TRACES" ]; then
    TRACES="Cache2k Wikipedia YouTube Zipf"
fi

# Run reports for each trace
for TRACE in $TRACES; do
    report $TRACE
done

# Create the combined report
create_combined_report