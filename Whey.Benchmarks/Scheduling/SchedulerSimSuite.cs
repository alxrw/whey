using Microsoft.Extensions.Logging.Abstractions;
using Whey.Benchmarks.Reporting;
using Whey.Infra.Services;

namespace Whey.Benchmarks.Scheduling;

// Question answered: for the GitHub API budget Whey spends, how quickly does it notice new releases,
// and how many installs get served a stale version, compared to just polling every package at one rate?
public static class SchedulerSimSuite
{
	// Exposed for `sim --check`.
	public static List<object> Summaries { get; private set; } = [];
	public static (int matches, int total) Fidelity { get; private set; }

	public static SuiteReport Run(bool quick)
	{
		int n = quick ? 2_000 : 10_000;
		int seeds = quick ? 2 : 5;
		int horizon = 180;

		var scenarios = new[]
		{
			new WorkloadConfig("mature-zipf", Packages: n),
			new WorkloadConfig("mature-release-rate-independent", Packages: n, ReleaseRateCorrelation: 0.0),
			new WorkloadConfig("cold-start-no-interactions", Packages: n, InteractionsRecorded: false),
		};

		var rep = new SuiteReport { Name = "scheduler-sim" };
		rep.Line("## Scheduler simulation: release-detection latency vs. GitHub API budget");
		rep.Line();
		rep.Line($"{n:N0} packages, {horizon}-day virtual horizon, {seeds} seeds per scenario (mean ± std). " +
			"Tier selection runs the real `ApiSchedulingService.SelectInterval`; everything else (release process, popularity, clock) is simulated.");
		rep.Line();

		var fidelity = FidelityCheck();
		rep.Line($"**Fidelity check:** simulator percentile + tier selection vs. the real `ApiSchedulingService.GetNextRun` on a SQLite database: " +
			$"{fidelity.matches}/{fidelity.total} packages got the identical interval.");
		rep.Line();

		var data = new List<object>();
		var scenarioSummaries = new List<object>();
		foreach (var cfg in scenarios)
		{
			var perSeed = new List<List<PolicyOutcome>>();
			for (int s = 0; s < seeds; s++)
			{
				var w = Workload.Generate(cfg, seed: 1000 + s);
				var tiered = Simulator.Run(w, new TieredPolicy(w), horizon, seed: s);
				var outcomes = new List<PolicyOutcome> { tiered };

				var matched = TimeSpan.FromDays(1.0 / tiered.CallsPerPackagePerDay);
				outcomes.Add(Simulator.Run(w, new UniformPolicy(matched, "Uniform, same API budget"), horizon, seed: s));
				foreach (var h in new[] { 1.0, 6.0, 24.0, 72.0 })
				{
					outcomes.Add(Simulator.Run(w, new UniformPolicy(TimeSpan.FromHours(h)), horizon, seed: s));
				}
				perSeed.Add(outcomes);
			}

			rep.Line($"### Scenario: `{cfg.Name}`");
			rep.Line();
			var names = perSeed[0].Select(o => o.Policy).ToArray();
			var rows = new List<string[]>();
			var scenarioData = new List<object>();
			for (int p = 0; p < names.Length; p++)
			{
				var os = perSeed.Select(x => x[p]).ToList();
				string F(Func<PolicyOutcome, double> f, string fmt = "0.00")
				{
					var (m, sd) = Stats.MeanStd(os.Select(f));
					return $"{m.ToString(fmt)} ± {sd.ToString(fmt)}";
				}
				rows.Add([
					names[p],
					F(o => o.CallsPerPackagePerDay),
					F(o => o.DelayMeanHours, "0.0"),
					F(o => o.DelayP95Hours, "0.0"),
					F(o => o.WeightedDelayP95Hours, "0.0"),
					F(o => o.StaleInstallPercent, "0.000"),
				]);
				scenarioData.Add(new { policy = names[p], runs = os });
			}
			rep.Table(
				["Policy", "API calls / pkg / day", "Detect delay mean (h)", "p95 (h)", "p95 install-weighted (h)", "Installs on stale version (%)"],
				rows);

			double Mean(Func<List<PolicyOutcome>, double> f) => Stats.MeanStd(perSeed.Select(f)).mean;
			var tieredStale = Mean(x => x[0].StaleInstallPercent);
			var matchedStale = Mean(x => x[1].StaleInstallPercent);
			var hourlyStale = Mean(x => x[2].StaleInstallPercent);
			var tieredDelay = Mean(x => x[0].DelayMeanHours);
			var matchedDelay = Mean(x => x[1].DelayMeanHours);
			var tieredCalls = Mean(x => x[0].CallsPerPackagePerDay);
			var hourlyCalls = Mean(x => x[2].CallsPerPackagePerDay);
			// Seeds differ a lot in absolute staleness (a few head packages carry most installs), so compare per seed.
			var (redMean, redStd) = Stats.MeanStd(perSeed.Select(x => 1 - x[0].StaleInstallPercent / x[1].StaleInstallPercent));

			{
				string delayNote = tieredDelay <= matchedDelay
					? $"and cuts mean per-release detection delay by {Pct(1 - tieredDelay / matchedDelay)}"
					: $"but its unweighted mean per-release detection delay is {Pct(tieredDelay / matchedDelay - 1)} higher ({tieredDelay:0.0}h vs {matchedDelay:0.0}h), because low-traffic packages are polled less often";
				rep.Line($"- At equal API budget, tiering serves **{Pct(redMean)} ± {Pct(redStd)}** fewer installs from a stale version (paired per seed; mean {tieredStale:0.00}% vs {matchedStale:0.00}%), {delayNote}.");
				rep.Line($"- Versus polling everything hourly ({hourlyStale:0.00}% stale installs), tiering uses **{Pct(1 - tieredCalls / hourlyCalls)}** fewer API calls at {tieredStale:0.00}% stale installs.");
			}
			scenarioSummaries.Add(new { scenario = cfg.Name, staleReductionVsEqualBudget = redMean, staleReductionStd = redStd, apiCallSavingsVsHourly = 1 - tieredCalls / hourlyCalls });
			rep.Line();

			data.Add(new { scenario = cfg, policies = scenarioData });
		}

		rep.Data = new { env = EnvInfo.Describe(), horizonDays = horizon, seeds, fidelity, summaries = scenarioSummaries, scenarios = data };
		Summaries = scenarioSummaries;
		Fidelity = fidelity;
		return rep;
	}

	private static string Pct(double x) => $"{x * 100:0.#}%";

	// Seed a real DB and make sure the simulator's view of the scheduler matches the real service.
	private static (int matches, int total) FidelityCheck()
	{
		const int n = 400;
		var w = Workload.Generate(new WorkloadConfig("fidelity", Packages: n), seed: 7);
		var rng = new Random(7);
		var ages = Enumerable.Range(0, n).Select(_ => rng.NextDouble() * 500).ToArray();

		var path = Path.Combine(Path.GetTempPath(), $"whey-fidelity-{Guid.NewGuid():N}.db");
		try
		{
			using var db = SqliteWheyContext.Create(path);
			db.Seed(w.Interactions, ages);
			var svc = new ApiSchedulingService(db, NullLogger<ApiSchedulingService>.Instance);

			var pkgs = db.Packages.AsEnumerable().OrderBy(p => int.Parse(p.Owner["owner".Length..])).ToList();
			int ok = 0;
			for (int i = 0; i < n; i++)
			{
				var real = svc.GetNextRun(pkgs[i]) - DateTimeOffset.UtcNow;
				var expected = ApiSchedulingService.SelectInterval(w.Percentile[i], ages[i]);
				if (Math.Abs((real - expected).TotalMinutes) < 1) ok++;
			}
			return (ok, n);
		}
		finally
		{
			foreach (var f in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(path) + "*")) File.Delete(f);
		}
	}
}
