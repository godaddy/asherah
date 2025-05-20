# SessionFactory Performance Report

This package benchmarks the performance of the `SessionFactory` struct and
its dependencies. It compares Metastore and KMS access patterns with
different cache configurations.

## Traces

Name         | Source
------------ | ------
Glimpse      | Authors of the LIRS algorithm - retrieved from [Cache2k](https://github.com/cache2k/cache2k-benchmark)
Multi2       | Authors of the LIRS algorithm - retrieved from [Cache2k](https://github.com/cache2k/cache2k-benchmark)
OLTP         | Authors of the ARC algorithm - retrieved from [Cache2k](https://github.com/cache2k/cache2k-benchmark)
ORMBusy      | GmbH - retrieved from [Cache2k](https://github.com/cache2k/cache2k-benchmark)
Sprite       | Authors of the LIRS algorithm - retrieved from [Cache2k](https://github.com/cache2k/cache2k-benchmark)
Wikipedia    | [WikiBench](http://www.wikibench.eu/)
YouTube      | [University of Massachusetts](http://traces.cs.umass.edu/index.php/Network/Network)
WebSearch    | [University of Massachusetts](http://traces.cs.umass.edu/index.php/Storage/Storage)

## Usage

1. Download trace files using the scripts in the `data` directory
2. Run tests with the command: `cargo test --release --features "test-traces"`
3. Run the enhanced report generation script: `./report.sh`
   - This will run all tests and generate comprehensive visualizations
   - Creates an HTML dashboard with all visualizations in the `out` directory

## Available Visualizations

1. **Request Impact**: Shows how operation rate changes with request count
   - `./visualize-request.sh out/request_*.txt`

2. **Cache Size Impact**: Shows how operation rate changes with different cache sizes
   - `./visualize-size.sh out/size_*.txt`

3. **KMS vs Metastore Operations**: Compares KMS and Metastore operation counts
   - `./visualize-kms-vs-metastore.sh out/request_*.txt`

4. **Latency Distribution**: Shows latency percentiles (min, avg, p50, p95, p99)
   - `./visualize-latency.sh out/latency_*.txt`

5. **Memory Usage**: Tracks heap, stack, and total memory usage
   - `./visualize-memory.sh out/memory_*.txt`

6. **Combined Visualizations**: Creates grid layouts of multiple visualizations
   - `./combine-png.sh out/*.png`

## Structure

- `src/lib.rs` - Core implementation of trace providers and reporting
- `src/cache2k.rs` - Provider for Cache2k traces
- `src/wikipedia.rs` - Provider for Wikipedia traces
- `src/youtube.rs` - Provider for YouTube traces
- `src/storage.rs` - Provider for storage traces
- `src/zipf.rs` - Zipf distribution implementation
- `src/report.rs` - Reporting and metrics collection
- `src/latency.rs` - Latency tracking and analysis
- `src/memory.rs` - Memory usage tracking and analysis
- `src/files.rs` - File utilities
- `tests/` - Test implementations for each type of trace

## Enhanced Visualization Capabilities

### The HTML Dashboard

The generated HTML dashboard provides comprehensive analysis including:

1. **Main Dashboard**: An overview of all trace results with combined visualizations
2. **Individual Trace Analysis**: Detailed metrics for each trace type
3. **Metric Comparisons**: Side-by-side comparisons of different caching strategies

### Additional Analysis Features

- **Cache Policy Comparison**: Compare different cache eviction policies like LRU, LFU, and TinyLFU
- **Resource Usage**: Track and visualize KMS and Metastore utilization
- **Latency Profiling**: Analyze operation latency distributions
- **Memory Profiling**: Monitor memory usage across different workloads
- **Scalability Analysis**: Understand how the system scales with increased request volume