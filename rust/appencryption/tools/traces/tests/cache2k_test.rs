#[cfg(feature = "test-traces")]
mod tests {
    use std::fs::create_dir_all;
    use std::path::Path;
    use std::sync::Arc;

    use asherah_traces::{
        files,
        report::{self, DelayedMetastore, FileReporter, TrackedKMS},
        Options, POLICIES,
    };

    async fn test_request(
        provider_fn: fn(Arc<dyn files::FileReader>) -> Arc<dyn asherah_traces::Provider>,
        opt: Options,
        trace_files: &str,
        report_file: &str,
    ) {
        let out_dir = Path::new("out");
        if !out_dir.exists() {
            create_dir_all(out_dir).expect("Failed to create output directory");
        }

        let reader = match files::open_files_glob(Path::new("data").join(trace_files)) {
            Ok(r) => r,
            Err(_) => {
                eprintln!("Skipping test, trace file not found: {}", trace_files);
                return;
            }
        };

        let provider = provider_fn(reader);

        let report_path = out_dir.join(report_file);
        let mut reporter = FileReporter::new(report_path).expect("Failed to create reporter");

        let kms = Arc::new(TrackedKMS::new());
        let metastore = Arc::new(DelayedMetastore::new(5, 5));

        report::benchmark_session_factory(provider, &mut reporter, &opt, kms, metastore).await;
    }

    async fn test_size(
        provider_fn: fn(Arc<dyn files::FileReader>) -> Arc<dyn asherah_traces::Provider>,
        mut opt: Options,
        trace_files: &str,
        report_file: &str,
    ) {
        let out_dir = Path::new("out");
        if !out_dir.exists() {
            create_dir_all(out_dir).expect("Failed to create output directory");
        }

        let report_path = out_dir.join(report_file);
        let mut reporter = FileReporter::new(report_path).expect("Failed to create reporter");

        for _ in 0..5 {
            let reader = match files::open_files_glob(Path::new("data").join(trace_files)) {
                Ok(r) => r,
                Err(_) => {
                    eprintln!("Skipping test, trace file not found: {}", trace_files);
                    return;
                }
            };

            let provider = provider_fn(reader.clone());
            let kms = Arc::new(TrackedKMS::new());
            let metastore = Arc::new(DelayedMetastore::new(5, 5));

            report::benchmark_session_factory(provider, &mut reporter, &opt, kms, metastore).await;

            // Double the cache size for the next iteration
            opt.cache_size *= 2;

            // Reset the reader for the next iteration
            let mut r = reader;
            if let Some(r) = Arc::get_mut(&mut r) {
                if let Err(e) = r.reset() {
                    eprintln!("Failed to reset reader: {}", e);
                    return;
                }
            }
        }
    }

    // Helper to create a Cache2k provider
    fn create_cache2k_provider(
        reader: Arc<dyn files::FileReader>,
    ) -> Arc<dyn asherah_traces::Provider> {
        Arc::new(asherah_traces::cache2k::Cache2kProvider::new(reader))
    }

    // Tests for request-based benchmarks

    #[tokio::test]
    async fn test_request_glimpse() {
        for policy in POLICIES {
            let opt = Options {
                policy: policy.to_string(),
                cache_size: 512,
                report_interval: 100,
                max_items: 6000,
            };

            test_request(
                create_cache2k_provider,
                opt,
                "trace-glimpse.trc.bin.gz",
                &format!("request_glimpse-{}.txt", policy),
            )
            .await;
        }
    }

    #[tokio::test]
    async fn test_request_oltp() {
        for policy in POLICIES {
            let opt = Options {
                policy: policy.to_string(),
                cache_size: 1000,
                report_interval: 1000,
                max_items: 90000,
            };

            test_request(
                create_cache2k_provider,
                opt,
                "trace-oltp.trc.bin.gz",
                &format!("request_oltp-{}.txt", policy),
            )
            .await;
        }
    }

    // Tests for size-based benchmarks

    #[tokio::test]
    async fn test_size_glimpse() {
        for policy in POLICIES {
            let opt = Options {
                policy: policy.to_string(),
                cache_size: 125,
                report_interval: 0,
                max_items: 6000,
            };

            test_size(
                create_cache2k_provider,
                opt,
                "trace-glimpse.trc.bin.gz",
                &format!("size_glimpse-{}.txt", policy),
            )
            .await;
        }
    }

    #[tokio::test]
    async fn test_size_oltp() {
        for policy in POLICIES {
            let opt = Options {
                policy: policy.to_string(),
                cache_size: 250,
                report_interval: 0,
                max_items: 50000,
            };

            test_size(
                create_cache2k_provider,
                opt,
                "trace-oltp.trc.bin.gz",
                &format!("size_oltp-{}.txt", policy),
            )
            .await;
        }
    }
}
