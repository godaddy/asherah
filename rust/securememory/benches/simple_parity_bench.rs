use criterion::{black_box, criterion_group, criterion_main, Criterion};
use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{SecretExtensions, SecretFactory};

fn bench_sequential_access(c: &mut Criterion) {
    let factory = DefaultSecretFactory::new();
    let mut data = b"thisismy32bytesecretthatiwilluse".to_vec();
    let secret = factory.new(&mut data).unwrap();

    c.bench_function("rust_with_bytes", |b| {
        b.iter(|| {
            secret
                .with_bytes(|bytes| {
                    black_box(bytes);
                    Ok(())
                })
                .unwrap()
        })
    });
}

criterion_group!(benches, bench_sequential_access);
criterion_main!(benches);
