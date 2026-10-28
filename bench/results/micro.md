## Micro-benchmarks (BenchmarkDotNet)

| Benchmark | Mean | Std dev | Ops/s (1 core) | Allocated/op |
|---|---|---|---|---|
| Classify one file (4-byte magic read) | 147.26 µs | 933.4 ns | 6,791 | 4,416 B |
| Ed25519 public-key import + verify (as the interceptor does) | 41.84 µs | 568.9 ns | 23,899 | 56 B |
| Ed25519 sign (client side) | 13.97 µs | 126.3 ns | 71,567 | 88 B |
| Ed25519 verify (per authenticated request) | 41.15 µs | 408.7 ns | 24,303 | 0 B |
| Parse objdump -p output (NEEDED regex) | 1.49 µs | 20.7 ns | 669,892 | 2,656 B |
| SelectReleaseAsset, 40-asset release | 5.15 µs | 44.9 ns | 193,996 | 10,920 B |
| SelectReleaseAsset, 5-asset release | 921.9 ns | 12.5 ns | 1,084,704 | 2,832 B |
| SHA-256 of 200 B request body | 413.3 ns | 3.0 ns | 2,419,366 | 56 B |

