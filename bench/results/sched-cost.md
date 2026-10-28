## Scheduling cost: JobInitializer-style full pass over all tracked packages

Real `ApiSchedulingService.GetNextRun` against a file-backed SQLite database with the production indexes. Per-call cost is measured over random samples; a full pass is `N × per-call`. The "single pass" column is a reference implementation (one SELECT of all interaction counts, sort, binary search) that yields identical tiers, to show what the cost floor is. SQLite is not Postgres; treat absolute numbers as indicative and the scaling as the signal.

| Packages | GetNextRun mean (ms) | p99 (ms) | Full pass, current (s) | Full pass, single query (ms) | Ratio |
|---|---|---|---|---|---|
| 1,000 | 0.35 | 0.55 | 0.4 | 31.5 | 11× |
| 5,000 | 0.50 | 1.07 | 2.5 | 9.6 | 263× |
| 20,000 | 0.40 | 0.61 | 8.0 | 43.0 | 187× |

Full-pass time scales as ~N^1.04 across this range (2.0 would be quadratic).

