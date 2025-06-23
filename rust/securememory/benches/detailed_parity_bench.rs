use criterion::{black_box, criterion_group, criterion_main, Criterion, Throughput};
use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{SecretExtensions, SecretFactory};

fn bench_sequential_access(c: &mut Criterion) {
    let mut group = c.benchmark_group("sequential_access");

    let factory = DefaultSecretFactory::new();
    let mut data = b"thisismy32bytesecretthatiwilluse".to_vec();
    let secret = factory.new(&mut data).unwrap();

    // Set throughput for all sequential benchmarks
    group.throughput(Throughput::Bytes(32));

    group.bench_function("rust_with_bytes", |b| {
        b.iter(|| {
            secret
                .with_bytes(|bytes| {
                    black_box(bytes);
                    Ok(())
                })
                .unwrap()
        })
    });

    // Test with different access patterns
    group.bench_function("rust_with_bytes_clone", |b| {
        b.iter(|| {
            let secret_clone = secret.clone();
            secret_clone
                .with_bytes(|bytes| {
                    black_box(bytes);
                    Ok(())
                })
                .unwrap()
        })
    });

    // Test access with computation
    group.bench_function("rust_with_bytes_compute", |b| {
        b.iter(|| {
            secret
                .with_bytes(|bytes| {
                    let sum = bytes.iter().sum::<u8>();
                    black_box(sum);
                    Ok(())
                })
                .unwrap()
        })
    });

    group.finish();
}

fn bench_multi_threaded_access(c: &mut Criterion) {
    // Skip concurrent tests for now since they're problematic
    let _ = c;
}

fn bench_allocation(c: &mut Criterion) {
    let mut group = c.benchmark_group("allocation");
    let factory = DefaultSecretFactory::new();

    // Bench different sizes
    for size in [32, 256, 1024, 4096].iter() {
        group.throughput(Throughput::Bytes(*size as u64));
        group.bench_with_input(format!("new_{}_bytes", size), size, |b, &size| {
            b.iter(|| {
                let mut data = vec![42u8; size];
                factory.new(&mut data).unwrap()
            })
        });
    }

    group.finish();
}

fn bench_memory_operations(c: &mut Criterion) {
    let mut group = c.benchmark_group("memory_operations");
    let factory = DefaultSecretFactory::new();

    // Test clone performance
    group.bench_function("clone", |b| {
        let mut data = b"thisismy32bytesecretthatiwilluse".to_vec();
        let secret = factory.new(&mut data).unwrap();
        b.iter(|| black_box(secret.clone()))
    });

    // Test drop performance
    group.bench_function("drop", |b| {
        b.iter(|| {
            let mut data = b"thisismy32bytesecretthatiwilluse".to_vec();
            let secret = factory.new(&mut data).unwrap();
            drop(secret);
        })
    });

    group.finish();
}

criterion_group!(
    benches,
    bench_sequential_access,
    bench_multi_threaded_access,
    bench_allocation,
    bench_memory_operations
);
criterion_main!(benches);
