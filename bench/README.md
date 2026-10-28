# Whey benchmarks

`Whey.Benchmarks` measures the parts of Whey that its design claims something about, using the real service code wherever possible.
Every suite answers a specific question; none of them exist to produce a big number.

```sh
dotnet run -c Release --project Whey.Benchmarks -- all          # everything, ~10 min, writes bench/results/
dotnet run -c Release --project Whey.Benchmarks -- sim          # one suite: sim | sched-cost | binary | auth-probes | grpc | micro
dotnet run -c Release --project Whey.Benchmarks -- all --quick  # smoke test with reduced sizes (don't publish these numbers)
dotnet run -c Release --project Whey.Benchmarks -- sim --check  # exit 3 if the scheduler no longer beats uniform polling at equal budget
```

Needs `objdump`, `readelf` and `file` on `PATH` (binutils, file). Latest committed results: [`results/summary.md`](results/summary.md); raw data is the adjacent `*.json`.

## What is measured, and why it matters

| Suite | Question | Headline metrics | Why it is not filler |
|---|---|---|---|
| `sim` | Does bucketed priority scheduling actually sync the packages that matter sooner, for the same GitHub API budget? | % of installs served a stale version; API calls / package / day; release-detection delay (mean, p95, install-weighted p95) vs. uniform polling at the *same* call budget | Compares against the baseline a reviewer would ask for, runs the real `ApiSchedulingService.SelectInterval`, and includes a cold-start scenario with no interaction data. Fidelity-checked against the real service on a database. |
| `sched-cost` | What does it cost to (re)schedule every package (startup, deploy)? | `GetNextRun` ms/call, full-pass seconds at 1k/5k/20k packages, vs. a single-query reference | `GetNextRun` issues 3 queries per package; this shows how that scales. |
| `binary` | How correct and fast is the binary analysis that runs on every release asset? | Precision/recall vs. `file(1)` on real binaries (ELF, PE, Mach-O), crash rate on real archives, files/s, descriptor leak, `objdump` agreement and latency | Ground truth is an independent tool on real files, not Whey's own helpers. |
| `auth-probes` | Does registration + proof-of-possession auth work, and are forged, tampered, replayed and stale requests rejected? | probes passed; attack requests rejected | Security controls are judged by what they reject, not how fast they accept. |
| `grpc` | Pipeline cost (with real signature verification), rate-limiter accuracy, per-key isolation | req/s, p50/p95/p99, admitted req/s vs. configured 5/s, victim latency/success while another key floods | Rate limiting is a security control, so the metric is whether it holds under abuse. |
| `micro` | CPU/alloc floors (Ed25519 verify, SHA-256, asset selection, objdump parse) | ns/op, ops/s/core, B/op | Gives the per-request auth cost ceiling and shows where time is *not* going. |

## Method and limits

- **Scheduler simulation** uses a virtual clock. Package popularity is Zipfian, releases are a Poisson process per package, and release rate is optionally correlated with popularity (`ReleaseRateCorrelation`; 0 means the scheduler gets no help from correlation). Each scenario runs 5 seeds; tables show mean ± std. The workload is synthetic: conclusions hold for the distribution assumed, so the parameters are in `Scheduling/Workload.cs` and the sensitivity scenarios are reported alongside the headline one. API-budget savings are about load and rate-limit headroom; GitHub does not charge authenticated 304 responses against the primary rate limit.
- **gRPC** runs in-process on `TestServer` with SQLite: it excludes network, TLS and Postgres latency, so absolute latencies are lower than production; relative results (limiter accuracy, isolation) are the point. The load generator shares the machine's CPU with the server. `ReportInstall` writes to the database, and SQLite serialises writers, so its throughput is a floor; Postgres will differ.
- **Scheduler cost** uses SQLite, not Postgres. Treat the scaling as the signal, not the milliseconds.
- **Binary corpus** is whatever real binaries exist on the machine running it (system ELF, .NET PE, Go's `debug/{macho,pe,elf}` testdata, macOS dylibs in the NuGet cache); counts vary by machine. Missing directories are skipped.
- Numbers depend on hardware. Compare runs from the same machine; the CI workflow uploads results as an artifact rather than committing them.

## Known limits of the auth scheme

- Nonce replay protection uses `IDistributedCache` get-then-set, which is not atomic: two simultaneous replays of one request can both pass. Closing this needs an atomic set-if-absent (e.g. Redis `SET NX`).
- Registration does not yet verify `release_signature` (existing TODO).

## Not covered

- `PackageSyncService.Sync` end to end (download → extract → analyse → upload): needs a storage fake that can hand out an Azure container client; `FakeBinStorageService` throws on `GetBinStorageServiceClient`.
- Real-network GitHub API latency and Azure Blob throughput.
- Postgres query performance (see `sched-cost` caveat).
