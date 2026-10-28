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

