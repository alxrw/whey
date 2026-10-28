using Whey.Infra.Services;

namespace Whey.Benchmarks.Scheduling;

public interface IPollPolicy
{
	string Name { get; }
	// Interval until the next poll, given what Whey currently believes about the package.
	TimeSpan NextInterval(int pkg, double knownAgeDays);
	// Jitter applied to each scheduled run, in seconds (+/-).
	int JitterSeconds { get; }
}

// The production policy: calls the real tier-selection function in ApiSchedulingService.
public sealed class TieredPolicy(Workload w) : IPollPolicy
{
	public string Name => "Whey tiered";
	public int JitterSeconds => 6 * 60; // ApiSchedulingService.MinsJitter
	public TimeSpan NextInterval(int pkg, double knownAgeDays) =>
		ApiSchedulingService.SelectInterval(w.Percentile[pkg], knownAgeDays);
}

public sealed class UniformPolicy(TimeSpan interval, string? name = null) : IPollPolicy
{
	public string Name => name ?? $"Uniform {Fmt(interval)}";
	public int JitterSeconds => 0;
	public TimeSpan NextInterval(int pkg, double knownAgeDays) => interval;
	private static string Fmt(TimeSpan t) => t.TotalHours < 48 ? $"{t.TotalHours:0.#}h" : $"{t.TotalDays:0.#}d";
}

public sealed record PolicyOutcome(
	string Policy,
	double CallsPerPackagePerDay,
	double DelayMeanHours,
	double DelayP50Hours,
	double DelayP95Hours,
	double DelayP99Hours,
	// install-weighted
	double WeightedDelayP95Hours,
	// expected share of installs served a version older than the latest published release
	double StaleInstallPercent,
	// share of unweighted package-time spent stale
	double StaleTimePercent,
	double UsefulPollPercent);

// Per-package event simulation with a virtual clock. Packages are independent given a static
// popularity percentile, so each is simulated on its own and results are merged.
public static class Simulator
{
	public static PolicyOutcome Run(Workload w, IPollPolicy policy, int horizonDays, int seed)
	{
		int n = w.N;
		long polls = 0, usefulPolls = 0;
		double weightedStale = 0, totalWeight = 0, unweightedStale = 0;
		var delaysByPkg = new (double[] delays, double rate)[n];
		var lockObj = new object();

		Parallel.For(0, n,
			() => (polls: 0L, useful: 0L, wStale: 0.0, stale: 0.0),
			(i, _, acc) =>
			{
				var rng = new Random(HashCode.Combine(seed, i));
				double rate = w.ReleasesPerDay[i];
				double horizon = horizonDays;

				// Releases: Poisson process, stationary start (age at t=0 ~ Exp(rate)).
				double initialAge = Exponential(rng, rate);
				double knownRelease = -initialAge;
				var releases = new List<double>();
				for (double t = Exponential(rng, rate); t < horizon; t += Exponential(rng, rate))
				{
					releases.Add(t);
				}

				var delays = new List<double>();
				int next = 0;                 // first release not yet seen by a poll
				double staleSince = double.NaN;
				double staleTotal = 0;
				long pc = 0, uc = 0;

				// Random initial phase so polls aren't synchronised.
				var first = policy.NextInterval(i, -knownRelease).TotalDays;
				double now = rng.NextDouble() * first;
				while (now < horizon)
				{
					pc++;
					bool found = false;
					while (next < releases.Count && releases[next] <= now)
					{
						if (double.IsNaN(staleSince)) staleSince = releases[next];
						knownRelease = releases[next];
						next++;
						found = true;
					}
					if (found)
					{
						uc++;
						double d = now - staleSince;
						delays.Add(d * 24.0);
						staleTotal += d;
						staleSince = double.NaN;
					}

					double interval = policy.NextInterval(i, now - knownRelease).TotalDays;
					if (policy.JitterSeconds > 0)
					{
						interval += (rng.NextDouble() * 2 - 1) * policy.JitterSeconds / 86400.0;
					}
					now += interval;
				}
				if (!double.IsNaN(staleSince))
				{
					staleTotal += horizon - staleSince; // never detected within horizon
				}

				delaysByPkg[i] = (delays.ToArray(), w.InstallsPerDay[i]);
				return (acc.polls + pc, acc.useful + uc, acc.wStale + staleTotal * w.InstallsPerDay[i], acc.stale + staleTotal);
			},
			acc =>
			{
				lock (lockObj)
				{
					polls += acc.polls;
					usefulPolls += acc.useful;
					weightedStale += acc.wStale;
					unweightedStale += acc.stale;
				}
			});

		totalWeight = w.InstallsPerDay.Sum();
		var all = new List<(double d, double wt)>();
		foreach (var (delays, rate) in delaysByPkg)
		{
			foreach (var d in delays) all.Add((d, rate));
		}
		all.Sort((a, b) => a.d.CompareTo(b.d));

		double Q(double p)
		{
			if (all.Count == 0) return double.NaN;
			return all[(int)Math.Min(all.Count - 1, Math.Floor(p * (all.Count - 1)))].d;
		}
		double WQ(double p)
		{
			if (all.Count == 0) return double.NaN;
			double tot = all.Sum(x => x.wt), cum = 0;
			foreach (var (d, wt) in all)
			{
				cum += wt;
				if (cum >= p * tot) return d;
			}
			return all[^1].d;
		}

		return new PolicyOutcome(
			policy.Name,
			CallsPerPackagePerDay: polls / (double)n / horizonDays,
			DelayMeanHours: all.Count == 0 ? double.NaN : all.Average(x => x.d),
			DelayP50Hours: Q(0.50),
			DelayP95Hours: Q(0.95),
			DelayP99Hours: Q(0.99),
			WeightedDelayP95Hours: WQ(0.95),
			StaleInstallPercent: 100.0 * weightedStale / (totalWeight * horizonDays),
			StaleTimePercent: 100.0 * unweightedStale / ((double)n * horizonDays),
			UsefulPollPercent: polls == 0 ? 0 : 100.0 * usefulPolls / polls);
	}

	private static double Exponential(Random rng, double rate) => -Math.Log(1.0 - rng.NextDouble()) / rate;
}
