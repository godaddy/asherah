# SessionFactory Performance Report

This package benchmarks the performance of the `SessionFactory` class and
its dependencies. It compares Metastore and KMS access patterns with
different cache configurations.

The source code for this package is derived from the package of the same
name in the [Mango Cache](https://github.com/goburrow/cache) project. See
[LICENSE](../../pkg/cache/internal/LICENSE) for copyright and
licensing information.

## Traces

Name         | Source
------------ | ------
Glimpse      | Authors of the LIRS algorithm - retrieved from [Cache2k](https://github.com/cache2k/cache2k-benchmark)
Multi2       | Authors of the LIRS algorithm - retrieved from [Cache2k](https://github.com/cache2k/cache2k-benchmark)
OLTP         | Authors of the ARC algorithm - retrieved from [Cache2k](https://github.com/cache2k/cache2k-benchmark)
ORMBusy      | GmbH - retrieved from [Cache2k](https://github.com/cache2k/cache2k-benchmark)
Sprite       | Authors of the LIRS algorithm - retrieved from [Cache2k](https://github.com/cache2k/cache2k-benchmark)
Wikipedia    | [WikiBench](http://www.wikibench.eu/)
YouTube      | [University of Massachusetts](http://traces.cs.umass.edu/index.php/Network/Network)
WebSearch    | [University of Massachusetts](http://traces.cs.umass.edu/index.php/Storage/Storage)
