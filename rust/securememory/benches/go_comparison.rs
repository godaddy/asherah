use criterion::{criterion_group, criterion_main, Criterion};
use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};
use std::io::Read;

// Benchmark with_bytes operation (similar to Go's BenchmarkProtectedMemorySecret_WithBytes)
fn bench_with_bytes(c: &mut Criterion) {
    c.bench_function("rust_with_bytes", |b| {
        let factory = DefaultSecretFactory;
        let mut data = vec![42u8; 32];
        let secret = factory.new(&mut data).unwrap();

        b.iter(|| {
            secret
                .with_bytes(|bytes| {
                    // Same as Go: just read the first byte
                    let _first = bytes[0];
                    Ok(())
                })
                .unwrap();
        });
    });
}

// Benchmark with_bytes_func operation (similar to Go's BenchmarkProtectedMemorySecret_WithBytesFunc)
fn bench_with_bytes_func(c: &mut Criterion) {
    c.bench_function("rust_with_bytes_func", |b| {
        let factory = DefaultSecretFactory;
        let mut data = vec![42u8; 32];
        let secret = factory.new(&mut data).unwrap();

        b.iter(|| {
            secret
                .with_bytes_func(|bytes| {
                    // Return a value, similar to Go
                    Ok((bytes[0], Vec::new()))
                })
                .unwrap();
        });
    });
}

// Benchmark read_all operation (similar to Go's BenchmarkProtectedMemoryReader_ReadAll)
fn bench_reader_read_all(c: &mut Criterion) {
    c.bench_function("rust_reader_read_all", |b| {
        let factory = DefaultSecretFactory;
        let mut data = vec![42u8; 32];
        let secret = factory.new(&mut data).unwrap();

        b.iter(|| {
            let mut reader = secret.reader().unwrap();
            let mut buf = vec![0u8; 32];
            reader.read_exact(&mut buf).unwrap();
        });
    });
}

// Benchmark sequential with_bytes operation (similar to Go's BenchmarkProtectedMemorySecret_WithBytes_Sequential)
fn bench_with_bytes_sequential(c: &mut Criterion) {
    c.bench_function("rust_with_bytes_sequential", |b| {
        let factory = DefaultSecretFactory;
        let mut data = vec![42u8; 32];
        let secret = factory.new(&mut data).unwrap();

        b.iter(|| {
            for _ in 0..10 {
                secret
                    .with_bytes(|bytes| {
                        let _first = bytes[0];
                        Ok(())
                    })
                    .unwrap();
            }
        });
    });
}

criterion_group!(
    benches,
    bench_with_bytes,
    bench_with_bytes_func,
    bench_reader_read_all,
    bench_with_bytes_sequential
);
criterion_main!(benches);
