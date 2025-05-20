use criterion::{criterion_group, criterion_main, BenchmarkId, Criterion};
use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretExtensions, SecretFactory};
use std::io::Read;

const KEY_SIZE: usize = 32;

fn bench_secret_with_bytes(c: &mut Criterion) {
    let factory = DefaultSecretFactory::new();
    let mut orig = b"thisismy32bytesecretthatiwilluse".to_vec();
    let copy_bytes = orig.clone();

    let secret = factory.new(&mut orig).unwrap();

    c.bench_function("secret_with_bytes", |b| {
        b.iter(|| {
            secret
                .with_bytes(|bytes| {
                    assert_eq!(bytes, copy_bytes.as_slice());
                    Ok(())
                })
                .unwrap()
        })
    });
}

fn bench_secret_with_bytes_func(c: &mut Criterion) {
    let factory = DefaultSecretFactory::new();
    let mut orig = b"thisismy32bytesecretthatiwilluse".to_vec();
    let copy_bytes = orig.clone();

    let secret = factory.new(&mut orig).unwrap();

    c.bench_function("secret_with_bytes_func", |b| {
        b.iter(|| {
            secret
                .with_bytes_func(|bytes| {
                    assert_eq!(bytes, copy_bytes.as_slice());
                    Ok(((), bytes.to_vec()))
                })
                .unwrap()
        })
    });
}

fn bench_secret_reader_read_all(c: &mut Criterion) {
    let factory = DefaultSecretFactory::new();
    let mut orig = b"thisismy32bytesecretthatiwilluse".to_vec();
    let copy_bytes = orig.clone();

    let secret = factory.new(&mut orig).unwrap();

    c.bench_function("secret_reader_read_all", |b| {
        b.iter(|| {
            let mut reader = secret.reader().unwrap();
            let mut buffer = vec![0u8; copy_bytes.len()];
            reader.read_exact(&mut buffer).unwrap();
            assert_eq!(buffer, copy_bytes);
        })
    });
}

fn bench_secret_reader_read_partial(c: &mut Criterion) {
    let factory = DefaultSecretFactory::new();
    let mut orig = b"thisismy32bytesecretthatiwilluse".to_vec();
    let copy_bytes = orig.clone();

    let secret = factory.new(&mut orig).unwrap();

    c.bench_function("secret_reader_read_partial", |b| {
        b.iter(|| {
            let mut reader = secret.reader().unwrap();
            let mut buffer = vec![0u8; 16]; // Read half the bytes
            reader.read_exact(&mut buffer).unwrap();
            assert_eq!(buffer, copy_bytes[..16].to_vec());
        })
    });
}

fn bench_create_random(c: &mut Criterion) {
    let factory = DefaultSecretFactory::new();

    c.bench_function("create_random", |b| {
        b.iter(|| {
            let secret = factory.create_random(KEY_SIZE).unwrap();
            secret
                .with_bytes(|bytes| {
                    assert_eq!(bytes.len(), KEY_SIZE);
                    Ok(())
                })
                .unwrap()
        })
    });
}

fn bench_lifecycle(c: &mut Criterion) {
    let factory = DefaultSecretFactory::new();

    c.bench_function("lifecycle", |b| {
        b.iter(|| {
            let mut orig = b"thisismy32bytesecretthatiwilluse".to_vec();
            let mut secret = factory.new(&mut orig).unwrap();

            secret.with_bytes(|_| Ok(())).unwrap();
            secret.close().unwrap();
        })
    });
}

fn bench_large_secret(c: &mut Criterion) {
    let sizes = [1024, 1024 * 10, 1024 * 100, 1024 * 1024]; // 1KB, 10KB, 100KB, 1MB
    let mut group = c.benchmark_group("large_secret_access");

    for size in sizes.iter() {
        let factory = DefaultSecretFactory::new();
        let secret = factory.create_random(*size).unwrap();

        group.bench_with_input(BenchmarkId::from_parameter(size), size, |b, &size| {
            b.iter(|| {
                secret
                    .with_bytes(|bytes| {
                        // Just access a few bytes to make sure it's working
                        let _ = bytes[0];
                        let _ = bytes[size / 2];
                        let _ = bytes[size - 1];
                        Ok(())
                    })
                    .unwrap()
            })
        });
    }

    group.finish();
}

fn bench_large_secret_reader(c: &mut Criterion) {
    let sizes = [1024, 1024 * 10, 1024 * 100]; // Skip 1MB to keep benches fast
    let mut group = c.benchmark_group("large_secret_reader");

    for size in sizes.iter() {
        let factory = DefaultSecretFactory::new();
        let secret = factory.create_random(*size).unwrap();

        group.bench_with_input(BenchmarkId::from_parameter(size), size, |b, &size| {
            b.iter(|| {
                let mut reader = secret.reader().unwrap();
                let mut total_bytes = 0;
                let mut buffer = vec![0u8; 8192]; // 8KB chunks

                loop {
                    match reader.read(&mut buffer) {
                        Ok(0) => break, // EOF
                        Ok(n) => {
                            total_bytes += n;
                        }
                        Err(e) => panic!("Read error: {}", e),
                    }
                }

                assert_eq!(total_bytes, size);
            })
        });
    }

    group.finish();
}

criterion_group!(
    benches,
    bench_secret_with_bytes,
    bench_secret_with_bytes_func,
    bench_secret_reader_read_all,
    bench_secret_reader_read_partial,
    bench_create_random,
    bench_lifecycle,
    bench_large_secret,
    bench_large_secret_reader
);
criterion_main!(benches);
