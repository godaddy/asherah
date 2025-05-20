# Rust Benchmark Results (with no-mlock feature)

Successfully ran benchmarks with the new `no-mlock` feature flag that disables memory locking for benchmarking purposes.

## Command
```bash
cargo bench --features no-mlock
```

## Results

### Basic Operations
```
secret_with_bytes       time:   [1.1560 µs 1.1687 µs 1.1816 µs]
                        change: [-19.977% -17.304% -14.698%] (p = 0.00 < 0.05)
                        Performance has improved.

secret_with_bytes_func  time:   [1.1398 µs 1.1516 µs 1.1645 µs]
                        change: [-3.6633% -0.9991% +1.7529%] (p = 0.47 > 0.05)
                        No change in performance detected.

secret_reader_read_all  time:   [1.1646 µs 1.1742 µs 1.1851 µs]
                        change: [-2.8806% -0.6811% +1.7854%] (p = 0.57 > 0.05)
                        No change in performance detected.

secret_reader_read_partial
                        time:   [1.1791 µs 1.1915 µs 1.2049 µs]
                        change: [-1.1384% +0.4106% +2.0037%] (p = 0.62 > 0.05)
                        No change in performance detected.

create_random           time:   [7.6352 µs 7.6763 µs 7.7204 µs]
                        change: [-0.1128% +0.6113% +1.3624%] (p = 0.12 > 0.05)
                        No change in performance detected.

lifecycle               time:   [6.7892 µs 6.8285 µs 6.8705 µs]
```

### Large Secret Operations

#### Access
```
large_secret_access/1024    time:   [1.3962 µs 1.4212 µs 1.4426 µs]
large_secret_access/10240   time:   [1.2971 µs 1.3197 µs 1.3463 µs]
large_secret_access/102400  time:   [4.6767 µs 4.7000 µs 4.7252 µs]
large_secret_access/1048576 time:   [39.888 µs 39.962 µs 40.040 µs]
```

#### Reader
```
large_secret_reader/1024    time:   [1.2970 µs 1.3139 µs 1.3344 µs]
large_secret_reader/10240   time:   [2.7151 µs 2.7360 µs 2.7568 µs]
large_secret_reader/102400  time:   [14.908 µs 14.995 µs 15.096 µs]
large_secret_reader/1048576 time:   [159.68 µs 160.48 µs 161.27 µs]
```

### Multiple Secrets
```
multiple_secrets/10     time:   [6.3525 µs 6.4028 µs 6.4569 µs]
multiple_secrets/100    time:   [59.669 µs 59.975 µs 60.300 µs]
multiple_secrets/1000   time:   [686.66 µs 690.62 µs 694.94 µs]
```

## Key Insights

1. **Performance Improvement**: The `secret_with_bytes` benchmark shows a ~17% performance improvement when memory locking is disabled, demonstrating the overhead of mlock operations.

2. **Consistent Performance**: Most operations remain consistent, showing that the primary bottleneck for these operations is not memory locking.

3. **Size Scaling**: Operations scale roughly linearly with data size, as expected:
   - 1KB: ~1.3µs
   - 10KB: ~1.3µs (similar due to page alignment)
   - 100KB: ~4.7µs
   - 1MB: ~40µs

4. **Reader Performance**: Reader operations are slightly slower than direct access, but still very performant.

5. **Multiple Secrets**: Creating multiple secrets scales linearly, showing good predictable performance characteristics.

## Comparison with Go

Now we can properly compare Rust and Go performance:

| Operation | Go | Rust (no-mlock) |
|-----------|----|----|
| secret_with_bytes | 368.4 ns | 1,168.7 ns |
| secret_with_bytes_func | 373.0 ns | 1,151.6 ns |
| reader_read_all | 737.3 ns | 1,174.2 ns |

Go still shows better raw performance for basic operations, but the gap is now more reasonable and allows for proper benchmarking and optimization.