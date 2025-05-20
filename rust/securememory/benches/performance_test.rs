use criterion::{criterion_group, criterion_main, Criterion, BenchmarkId};
use securememory::protected_memory::DefaultSecretFactory;
use securememory::secret::{Secret, SecretFactory, SecretExtensions};
use std::sync::Arc;
use std::thread;

fn bench_single_threaded(c: &mut Criterion) {
    let mut group = c.benchmark_group("single_threaded");
    
    group.bench_function("with_bytes", |b| {
        let factory = DefaultSecretFactory;
        let mut data = vec![42u8; 32];
        let secret = factory.new(&mut data).unwrap();
        
        b.iter(|| {
            secret.with_bytes(|bytes| {
                let _val = bytes[0];
                Ok(())
            }).unwrap();
        });
    });
    
    group.bench_function("is_closed_check", |b| {
        let factory = DefaultSecretFactory;
        let mut data = vec![42u8; 32];
        let secret = factory.new(&mut data).unwrap();
        
        b.iter(|| {
            secret.is_closed();
        });
    });
    
    group.finish();
}

fn bench_multi_threaded(c: &mut Criterion) {
    let mut group = c.benchmark_group("multi_threaded");
    
    for num_threads in [2, 4, 8].iter() {
        group.bench_with_input(BenchmarkId::from_parameter(num_threads), num_threads, |b, &num_threads| {
            let factory = DefaultSecretFactory;
            let mut data = vec![42u8; 32];
            let secret = Arc::new(factory.new(&mut data).unwrap());
            
            b.iter(|| {
                let mut handles = vec![];
                
                for _ in 0..num_threads {
                    let secret_clone = Arc::clone(&secret);
                    let handle = thread::spawn(move || {
                        for _ in 0..10 {
                            secret_clone.with_bytes(|bytes| {
                                let _val = bytes[0];
                                Ok(())
                            }).unwrap();
                        }
                    });
                    handles.push(handle);
                }
                
                for handle in handles {
                    handle.join().unwrap();
                }
            });
        });
    }
    
    group.finish();
}

criterion_group!(benches, bench_single_threaded, bench_multi_threaded);
criterion_main!(benches);