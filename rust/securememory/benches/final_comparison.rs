use criterion::{black_box, criterion_group, criterion_main, Criterion};
use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};
use std::io::Read;

fn benchmark_against_go(c: &mut Criterion) {
    let factory = DefaultSecretFactory::new();
    let test_data = vec![42u8; 1024];

    // Create a secret similar to the Go benchmark
    let mut data = test_data.clone();
    let secret = factory.new(&mut data).unwrap();

    // Benchmark with_bytes - should match Go's ~375 ns/op
    c.bench_function("rust_with_bytes", |b| {
        b.iter(|| {
            secret
                .with_bytes(|data| {
                    // Just access the first byte like Go benchmark
                    let _val = black_box(data[0]);
                    Ok(())
                })
                .unwrap();
        });
    });

    // Benchmark with_bytes_func - should match Go's ~377 ns/op
    c.bench_function("rust_with_bytes_func", |b| {
        b.iter(|| {
            let _ = secret
                .with_bytes_func(|data| {
                    let _val = black_box(data[0]);
                    Ok(((), vec![]))
                })
                .unwrap();
        });
    });

    // Benchmark reader - should match Go's ~725 ns/op
    c.bench_function("rust_reader_readall", |b| {
        b.iter(|| {
            let mut reader = secret.reader().unwrap();
            let mut buf = Vec::new();
            reader.read_to_end(&mut buf).unwrap();
            black_box(buf);
        });
    });

    // Summary
    println!("\n=== Performance Comparison ===");
    println!("Go with_bytes:        ~375 ns/op");
    println!("Go with_bytes_func:   ~377 ns/op");
    println!("Go reader_readall:    ~725 ns/op");
    println!("Target: Match or beat Go's performance");
}

criterion_group!(benches, benchmark_against_go);
criterion_main!(benches);
