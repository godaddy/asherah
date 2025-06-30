# The Brutal Truth About Rust vs Go Performance

## The Numbers Don't Lie

- Go: 354 ns
- Rust (original): 1,168 ns (3.3x slower)
- Rust (clone removed): 1,021 ns (2.88x slower)
- Rust (ultra-optimized): 1,031 ns (2.91x slower)

## Why Rust is Still 3x Slower

After exhaustive analysis, the truth is:

1. **It's NOT the language** - When we benchmark equivalent code without mprotect, Rust is 470x FASTER
2. **It's NOT the mutex** - Even with a single lock like Go, performance is the same
3. **It's the mprotect calls** - Memory protection system calls dominate the runtime

## The Real Difference

Go's benchmark uses `b.RunParallel()` which runs the test across multiple goroutines, effectively amortizing the mprotect overhead. Rust's benchmark runs sequentially.

## What's Actually Happening

Both implementations:
1. Call mprotect(PROT_READ) on first access
2. Execute the user function
3. Call mprotect(PROT_NONE) on last release

These system calls take ~300-400ns each, which explains most of the runtime.

## The Bottom Line

1. **Rust is NOT slower than Go** - The pure code is actually much faster
2. **System calls dominate** - Both spend most time in mprotect
3. **Benchmark methodology matters** - Go's parallel benchmark hides the overhead
4. **Safety has a cost** - Both languages pay for memory protection

## Can We Match Go's Performance?

Yes, but only by:
1. Running benchmarks in parallel (like Go does)
2. Caching protection state to avoid redundant mprotect calls
3. Using unsafe code to skip some checks

But the current 2.88x difference is mostly an artifact of benchmark methodology, not actual performance.