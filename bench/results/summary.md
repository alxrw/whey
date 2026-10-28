# Whey benchmark results

Generated 2026-10-28 on Ubuntu 22.04.5 LTS, 16 cores, .NET 10.0.13.

## Scheduler simulation: release-detection latency vs. GitHub API budget

10,000 packages, 180-day virtual horizon, 5 seeds per scenario (mean ± std). Tier selection runs the real `ApiSchedulingService.SelectInterval`; everything else (release process, popularity, clock) is simulated.

**Fidelity check:** simulator percentile + tier selection vs. the real `ApiSchedulingService.GetNextRun` on a SQLite database: 400/400 packages got the identical interval.

### Scenario: `mature-zipf`

| Policy | API calls / pkg / day | Detect delay mean (h) | p95 (h) | p95 install-weighted (h) | Installs on stale version (%) |
|---|---|---|---|---|---|
| Whey tiered | 10.28 ± 0.07 | 1.6 ± 0.0 | 5.5 ± 0.0 | 1.0 ± 0.0 | 2.761 ± 1.279 |
| Uniform, same API budget | 10.28 ± 0.07 | 1.2 ± 0.0 | 2.2 ± 0.0 | 2.2 ± 0.0 | 5.450 ± 2.200 |
| Uniform 1h | 24.00 ± 0.00 | 0.5 ± 0.0 | 1.0 ± 0.0 | 1.0 ± 0.0 | 2.685 ± 1.281 |
| Uniform 6h | 4.00 ± 0.00 | 3.1 ± 0.0 | 5.7 ± 0.0 | 5.8 ± 0.1 | 10.735 ± 2.902 |
| Uniform 24h | 1.00 ± 0.00 | 12.5 ± 0.0 | 23.0 ± 0.0 | 23.6 ± 0.2 | 24.101 ± 2.577 |
| Uniform 3d | 0.33 ± 0.00 | 38.8 ± 0.1 | 69.3 ± 0.0 | 71.4 ± 0.3 | 38.576 ± 2.000 |

- At equal API budget, tiering serves **50.3% ± 3.2%** fewer installs from a stale version (paired per seed; mean 2.76% vs 5.45%), but its unweighted mean per-release detection delay is 34.3% higher (1.6h vs 1.2h), because low-traffic packages are polled less often.
- Versus polling everything hourly (2.68% stale installs), tiering uses **57.2%** fewer API calls at 2.76% stale installs.

### Scenario: `mature-release-rate-independent`

| Policy | API calls / pkg / day | Detect delay mean (h) | p95 (h) | p95 install-weighted (h) | Installs on stale version (%) |
|---|---|---|---|---|---|
| Whey tiered | 10.91 ± 0.07 | 1.7 ± 0.0 | 5.5 ± 0.0 | 3.0 ± 0.7 | 0.157 ± 0.019 |
| Uniform, same API budget | 10.91 ± 0.07 | 1.1 ± 0.0 | 2.1 ± 0.0 | 2.1 ± 0.0 | 0.216 ± 0.040 |
| Uniform 1h | 24.00 ± 0.00 | 0.5 ± 0.0 | 1.0 ± 0.0 | 0.9 ± 0.0 | 0.096 ± 0.016 |
| Uniform 6h | 4.00 ± 0.00 | 3.0 ± 0.0 | 5.7 ± 0.0 | 5.7 ± 0.1 | 0.601 ± 0.123 |
| Uniform 24h | 1.00 ± 0.00 | 12.3 ± 0.0 | 22.9 ± 0.0 | 22.9 ± 0.2 | 2.217 ± 0.372 |
| Uniform 3d | 0.33 ± 0.00 | 38.1 ± 0.1 | 69.0 ± 0.0 | 69.4 ± 0.7 | 6.142 ± 1.138 |

- At equal API budget, tiering serves **26.6% ± 5.8%** fewer installs from a stale version (paired per seed; mean 0.16% vs 0.22%), but its unweighted mean per-release detection delay is 49.9% higher (1.7h vs 1.1h), because low-traffic packages are polled less often.
- Versus polling everything hourly (0.10% stale installs), tiering uses **54.6%** fewer API calls at 0.16% stale installs.

### Scenario: `cold-start-no-interactions`

| Policy | API calls / pkg / day | Detect delay mean (h) | p95 (h) | p95 install-weighted (h) | Installs on stale version (%) |
|---|---|---|---|---|---|
| Whey tiered | 9.92 ± 0.08 | 1.7 ± 0.0 | 5.6 ± 0.0 | 1.0 ± 0.0 | 2.826 ± 1.276 |
| Uniform, same API budget | 9.92 ± 0.08 | 1.2 ± 0.0 | 2.3 ± 0.0 | 2.3 ± 0.0 | 5.605 ± 2.221 |
| Uniform 1h | 24.00 ± 0.00 | 0.5 ± 0.0 | 1.0 ± 0.0 | 1.0 ± 0.0 | 2.685 ± 1.281 |
| Uniform 6h | 4.00 ± 0.00 | 3.1 ± 0.0 | 5.7 ± 0.0 | 5.8 ± 0.1 | 10.735 ± 2.902 |
| Uniform 24h | 1.00 ± 0.00 | 12.5 ± 0.0 | 23.0 ± 0.0 | 23.6 ± 0.2 | 24.101 ± 2.577 |
| Uniform 3d | 0.33 ± 0.00 | 38.8 ± 0.1 | 69.3 ± 0.0 | 71.4 ± 0.3 | 38.576 ± 2.000 |

- At equal API budget, tiering serves **50.4% ± 3.1%** fewer installs from a stale version (paired per seed; mean 2.83% vs 5.60%), but its unweighted mean per-release detection delay is 41.2% higher (1.7h vs 1.2h), because low-traffic packages are polled less often.
- Versus polling everything hourly (2.68% stale installs), tiering uses **58.6%** fewer API calls at 2.83% stale installs.


## Scheduling cost: JobInitializer-style full pass over all tracked packages

Real `ApiSchedulingService.GetNextRun` against a file-backed SQLite database with the production indexes. Per-call cost is measured over random samples; a full pass is `N × per-call`. The "single pass" column is a reference implementation (one SELECT of all interaction counts, sort, binary search) that yields identical tiers, to show what the cost floor is. SQLite is not Postgres; treat absolute numbers as indicative and the scaling as the signal.

| Packages | GetNextRun mean (ms) | p99 (ms) | Full pass, current (s) | Full pass, single query (ms) | Ratio |
|---|---|---|---|---|---|
| 1,000 | 0.35 | 0.55 | 0.4 | 31.5 | 11× |
| 5,000 | 0.50 | 1.07 | 2.5 | 9.6 | 263× |
| 20,000 | 0.40 | 0.61 | 8.0 | 43.0 | 187× |

Full-pass time scales as ~N^1.04 across this range (2.0 would be quadratic).


## Binary analysis

### Classification accuracy vs `file(1)` (3,935 real files)

| Class | Files (truth) | Correct | False pos. | False neg. | Precision | Recall |
|---|---|---|---|---|---|---|
| Elf | 983 | 983 | 0 | 0 | 100.0 % | 100.0 % |
| Exe | 759 | 759 | 0 | 0 | 100.0 % | 100.0 % |
| Macho | 3 | 3 | 0 | 0 | 100.0 % | 100.0 % |

Non-binary files in corpus: 2,190. Files on which `GetBinaryExecutableType` **threw** instead of classifying: **0**.


### Edge cases (synthetic headers)

| Case | Expected | Actual |  |
|---|---|---|---|
| Mach-O 64 LE (thin) | Macho | Macho | pass |
| Mach-O universal / fat (0xCAFEBABE) | Macho | Macho | pass |
| Mach-O universal / fat64 (0xCAFEBABF) | Macho | Macho | pass |
| Plain text starting with "MZ" | Unknown | Unknown | pass |
| Java .class (0xCAFEBABE) | Unknown | Unknown | pass |
| Empty file (0 bytes) | Unknown | Unknown | pass |
| 3-byte file | Unknown | Unknown | pass |
| `FindBinaries` on a directory with one ELF and one empty `.keep` file | 1 binary found | 1 binary found | pass |

### `FindBinaries` throughput and file-handle behaviour

| Files in tree | Time (ms) | Files / s | Binaries found | Open FDs left (before GC) | (after GC) | Error |
|---|---|---|---|---|---|---|
| 1,000 | 7 | 137,722 | 50 | 0 | 0 |  |
| 10,000 | 90 | 111,565 | 500 | 0 | 0 |  |
| 50,000 | 422 | 118,408 | 2,500 | 0 | 0 |  |

`FindBinaries` over a 5,000-file tree with `ulimit -n 1024` (the common default): **ok, 250 binaries found**

Descriptors are released as each file is classified, so the count stays at zero regardless of the `ulimit -n` of the host (this machine: 1048576).

### Dependency extraction (`DependencyFinderService.GetLibsLinux`)

| ELF files | NEEDED set == readelf | With ≥1 dep | Latency p50 (ms) | p95 (ms) | Regex parse only (µs) | Process spawn share |
|---|---|---|---|---|---|---|
| 120 | 100% | 116 | 12.5 | 15.2 | 2.5 | 100% |

Each binary costs one `objdump` process spawn; the parse itself is noise by comparison. Agreement is against `readelf -d` (same binutils family), so it validates the parser, not objdump.


## Auth and registration probes

| Probe | Expected | Actual |  |
|---|---|---|---|
| Register first client | OK | OK | pass |
| Register a second, distinct client | OK | OK | pass |
| Re-register an existing key | AlreadyExists | AlreadyExists: public key is already registered | pass |
| Stored key is the client's public key | 54060F6B0FF0DB085B184356A9C5F1970EE4469EEE493310FDA205135B9FC8C2 | 54060F6B,679F86EC | pass |
| Correctly signed request from a registered client | OK | OK | pass |
| ReportInstall increments the package's TotalInteractions (scheduler input) | 1 | 1 | pass |
| ReportInstall for an untracked package | NotFound | NotFound: Package is not tracked. | pass |
| No credentials | Unauthenticated | Unauthenticated: Auth headers missing | pass |
| Signed body swapped after signing (tampered payload) | Unauthenticated | Unauthenticated: Invalid signature | pass |
| Signature made by a different key | Unauthenticated | Unauthenticated: Invalid signature | pass |
| Signature for a different method | Unauthenticated | Unauthenticated: Invalid signature | pass |
| Valid key + signature, unregistered client | Unauthenticated | Unauthenticated: Client unregistered | pass |
| Timestamp 10 minutes old (stale) | Unauthenticated | Unauthenticated: Stale or invalid timestamp | pass |
| Timestamp 10 minutes ahead | Unauthenticated | Unauthenticated: Stale or invalid timestamp | pass |
| Nonce used for the first time | OK | OK | pass |
| Exact replay of that request | Unauthenticated | Unauthenticated: Nonce already used | pass |
| Garbage public key / signature bytes | Unauthenticated | Unauthenticated: Invalid signature | pass |

**17/17 probes pass; 9/9 attack requests rejected.**


## gRPC pipeline and rate limiting

Real `Whey.Server` pipeline on an in-process TestServer + SQLite (no network/TLS/Postgres). Every authenticated call carries a real Ed25519 signature, nonce and timestamp, so the numbers include authentication.

### Pipeline latency and throughput

| RPC | Concurrency | Req/s | p50 (ms) | p95 (ms) | p99 (ms) | Errors |
|---|---|---|---|---|---|---|
| ReportInstall (auth + limiter + handler + EF) | 1 | 864 | 1.06 | 2.29 | 3.46 | 0 |
| ReportInstall (auth + limiter + handler + EF) | 16 | 2,211 | 0.85 | 5.21 | 152.48 | 0 |
| ReportInstall (auth + limiter + handler + EF) | 64 | 2,101 | 18.92 | 157.97 | 205.89 | 0 |
| Challenge (public path: skips auth + limiter) | 1 | 8,316 | 0.11 | 0.16 | 0.19 | 0 |
| Challenge (public path: skips auth + limiter) | 16 | 89,153 | 0.05 | 0.24 | 3.36 | 0 |
| Challenge (public path: skips auth + limiter) | 64 | 57,758 | 0.25 | 5.90 | 8.97 | 0 |

### Rate-limit accuracy (one key, 32 requests always in flight)

| Run (s) | Offered req/s | Admitted | Expected admitted | Steady admitted req/s (configured 5) | Rejected (RESOURCE_EXHAUSTED) | Other errors | Rejection latency p50 (ms) |
|---|---|---|---|---|---|---|---|
| 16.9 | 8,160 | 124 | 119–134 | 4.97 | 137,670 | 0 | 1.52 |

Admitted-request latency p50 2002 ms / p99 2019 ms (queued requests wait for tokens).

### Isolation: a well-behaved key while another key floods

| Victim (4 req/s) | Requests | Success | p50 (ms) | p99 (ms) |
|---|---|---|---|---|
| alone | 61 | 100% | 1.52 | 4.95 |
| while abuser sends 8,675 req/s | 57 | 100% | 8.30 | 92.59 |

Abuser: 125 admitted, 130,005 rejected.


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


