namespace Whey.Benchmarks.Scheduling;

public sealed record WorkloadConfig(
	string Name,
	int Packages = 10_000,
	double ZipfExponent = 1.0,
	// installs/day of the most popular package
	double TopInstallsPerDay = 2000,
	// days of history behind Package stats TotalInteractions
	int ObservationDays = 30,
	// false => cold start: no installs reported yet, so TotalInteractions is 0 everywhere
	bool InteractionsRecorded = true,
	// corr(log release rate, log popularity)
	double ReleaseRateCorrelation = 0.5,
	double MedianReleaseIntervalDays = 45,
	double ReleaseIntervalSigma = 1.2);

// Synthetic-but-parameterised package population. Nothing here depends on the scheduler under test.
public sealed class Workload
{
	public required WorkloadConfig Config { get; init; }
	public required double[] InstallsPerDay { get; init; }
	public required long[] Interactions { get; init; }
	// Mirrors ApiSchedulingService.GetNextRun: 1 - (#packages with strictly more interactions / N), 0 if never interacted with
	public required double[] Percentile { get; init; }
	public required double[] ReleasesPerDay { get; init; }

	public int N => InstallsPerDay.Length;

	public static Workload Generate(WorkloadConfig c, int seed)
	{
		var rng = new Random(seed);
		int n = c.Packages;

		// Popularity ranks are assigned to a random permutation so index carries no information.
		var perm = Enumerable.Range(0, n).OrderBy(_ => rng.Next()).ToArray();
		var installs = new double[n];
		for (int rank = 1; rank <= n; rank++)
		{
			installs[perm[rank - 1]] = c.TopInstallsPerDay / Math.Pow(rank, c.ZipfExponent);
		}

		var logPop = installs.Select(x => Math.Log(x)).ToArray();
		double mu = logPop.Average();
		double sd = Math.Sqrt(logPop.Sum(x => (x - mu) * (x - mu)) / n);

		var releaseRate = new double[n];
		double rho = c.ReleaseRateCorrelation;
		for (int i = 0; i < n; i++)
		{
			double zPop = (logPop[i] - mu) / sd;
			double z = rho * zPop + Math.Sqrt(1 - rho * rho) * Gaussian(rng);
			double interval = c.MedianReleaseIntervalDays * Math.Exp(-c.ReleaseIntervalSigma * z);
			releaseRate[i] = 1.0 / interval;
		}

		var interactions = new long[n];
		if (c.InteractionsRecorded)
		{
			for (int i = 0; i < n; i++)
			{
				interactions[i] = (long)Math.Floor(installs[i] * c.ObservationDays);
			}
		}

		return new Workload
		{
			Config = c,
			InstallsPerDay = installs,
			Interactions = interactions,
			Percentile = PercentileOf(interactions),
			ReleasesPerDay = releaseRate,
		};
	}

	public static double[] PercentileOf(long[] interactions)
	{
		int n = interactions.Length;
		var sorted = (long[])interactions.Clone();
		Array.Sort(sorted);
		var p = new double[n];
		for (int i = 0; i < n; i++)
		{
			// count strictly greater = n - upperBound(x)
			int ub = UpperBound(sorted, interactions[i]);
			p[i] = Whey.Infra.Services.ApiSchedulingService.InteractionPercentile(interactions[i], n - ub, n);
		}
		return p;
	}

	private static int UpperBound(long[] sorted, long x)
	{
		int lo = 0, hi = sorted.Length;
		while (lo < hi)
		{
			int mid = (lo + hi) >>> 1;
			if (sorted[mid] <= x) lo = mid + 1; else hi = mid;
		}
		return lo;
	}

	internal static double Gaussian(Random rng)
	{
		double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
		return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
	}
}
