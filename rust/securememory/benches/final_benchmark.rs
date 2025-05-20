use criterion::{black_box, criterion_group, criterion_main, Criterion};
use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{SecretFactory, SecretExtensions};
use std::sync::Arc;
use std::thread;
use std::time::Instant;

/// Benchmark that shows our final optimized implementation
fn bench_final_implementation(c: &mut Criterion) {
    // Sequential benchmark (matches Rust's default)
    let factory = DefaultSecretFactory::new();
    let data = b"thisismy32bytesecretthatiwilluse";
    let copy_bytes = data.to_vec();
    
    let mut orig = data.to_vec();
    let secret = factory.new(&mut orig).unwrap();
    
    c.bench_function("final_sequential", |b| {
        b.iter(|| {
            secret.with_bytes(|bytes| {
                black_box(bytes);
                assert_eq!(bytes, copy_bytes.as_slice());
                Ok(())
            }).unwrap()
        })
    });
    
    // Parallel benchmark (matches Go's default)
    let factory = DefaultSecretFactory::new();
    let mut orig = data.to_vec();
    let secret = Arc::new(factory.new(&mut orig).unwrap());
    let copy_bytes = data.to_vec();
    
    c.bench_function("final_parallel", move |b| {
        b.iter_custom(|iters| {
            let start = Instant::now();
            
            // Match Go's RunParallel behavior
            let num_threads = num_cpus::get();
            let iters_per_thread = iters / num_threads as u64;
            
            let threads: Vec<_> = (0..num_threads)
                .map(|_| {
                    let secret = Arc::clone(&secret);
                    let copy_bytes = copy_bytes.clone();
                    thread::spawn(move || {
                        for _ in 0..iters_per_thread {
                            secret.with_bytes(|bytes| {
                                assert_eq!(bytes, copy_bytes.as_slice());
                                Ok(())
                            }).unwrap();
                        }
                    })
                })
                .collect();
            
            for thread in threads {
                thread.join().unwrap();
            }
            
            start.elapsed()
        });
    });
}

criterion_group!(benches, bench_final_implementation);
criterion_main!(benches);