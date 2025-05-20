use criterion::{black_box, criterion_group, criterion_main, Criterion, Throughput};
use memguard::{Stream, DEFAULT_STREAM_CHUNK_SIZE};
use std::io::{Read, Write};

fn benchmark_stream_write(c: &mut Criterion) {
    let data_chunk: Vec<u8> = vec![0; DEFAULT_STREAM_CHUNK_SIZE];

    let mut group = c.benchmark_group("StreamWrite");
    group.throughput(Throughput::Bytes(data_chunk.len() as u64));
    group.bench_function("write_one_chunk", |b| {
        b.iter_batched(
            || (Stream::new(), data_chunk.clone()), // Setup: new stream and data per batch
            |(mut s, data)| {
                s.write_all(black_box(&data)).unwrap();
            },
            criterion::BatchSize::SmallInput, // Adjust batch size as needed
        )
    });
    group.finish();
}

fn benchmark_stream_read_batched(c: &mut Criterion) {
    let data_chunk: Vec<u8> = vec![0; DEFAULT_STREAM_CHUNK_SIZE];
    let mut read_buffer = vec![0; DEFAULT_STREAM_CHUNK_SIZE];

    let mut group = c.benchmark_group("StreamRead");
    group.throughput(Throughput::Bytes(data_chunk.len() as u64));

    // Benchmark reading one chunk at a time
    group.bench_function("read_one_chunk", |b| {
        b.iter_batched(
            || { // Setup for each iteration
                let mut s = Stream::new();
                s.write_all(&data_chunk).unwrap(); // Pre-fill the stream with one chunk
                s
            },
            |mut s| { // Routine to benchmark
                s.read_exact(black_box(&mut read_buffer)).unwrap();
            },
            criterion::BatchSize::SmallInput, // Each iteration gets a fresh stream with one chunk
        )
    });
    group.finish();
}

criterion_group!(benches, benchmark_stream_write, benchmark_stream_read_batched);
criterion_main!(benches);
