# Whey

Whey is a backend registry service for [Parm](https://github.com/alxrw/parm). It currently does the following:
- Acts as a mirror for Parm clients to install packages from Whey instead of GitHub
- Tracks install statistics/analytics for repositories
- Pre-computes client-side operations such as dependency finding and release asset selection.
- Allows expansion of parm into other vendors (e.g. Docker, GitLab)

## Running Whey

Before running, ensure you have Docker and Docker Compose installed.

Run `docker-compose up --build -d`

## Benchmarks

`Whey.Benchmarks` measures scheduler effectiveness, binary-analysis correctness, gRPC rate limiting and hot-path costs. See [`bench/README.md`](bench/README.md) for what each metric means and how to run it, and [`bench/results/summary.md`](bench/results/summary.md) for the latest numbers.
