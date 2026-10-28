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

