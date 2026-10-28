using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Whey.Benchmarks.Reporting;
using Whey.Infra.Services;

namespace Whey.Benchmarks.Scheduling;

// Question answered: how long does it take to (re)schedule every tracked package, e.g. JobInitializer on
// startup or after a deploy? GetNextRun issues Find + COUNT(>x) + COUNT(*) per package, and the COUNT(>x)
// counts every row above x, so per-call cost grows with N and a full pass is superlinear.
public static class SchedulerCostSuite
{
	public static SuiteReport Run(bool quick)
	{
		int[] sizes = quick ? [500, 2_000] : [1_000, 5_000, 20_000];
		const int sampleCalls = 200;

		var rep = new SuiteReport { Name = "sched-cost" };
		rep.Line("## Scheduling cost: JobInitializer-style full pass over all tracked packages");
		rep.Line();
		rep.Line("Real `ApiSchedulingService.GetNextRun` against a file-backed SQLite database with the production indexes. " +
			"Per-call cost is measured over random samples; a full pass is `N × per-call`. " +
			"The \"single pass\" column is a reference implementation (one SELECT of all interaction counts, sort, binary search) " +
			"that yields identical tiers, to show what the cost floor is. SQLite is not Postgres; treat absolute numbers as indicative and the scaling as the signal.");
		rep.Line();

		var rows = new List<string[]>();
		var data = new List<object>();
		foreach (int n in sizes)
		{
			var w = Workload.Generate(new WorkloadConfig("cost", Packages: n), seed: 3);
			var rng = new Random(3);
			var ages = Enumerable.Range(0, n).Select(_ => rng.NextDouble() * 400).ToArray();
			var path = Path.Combine(Path.GetTempPath(), $"whey-cost-{Guid.NewGuid():N}.db");
			try
			{
				using var db = SqliteWheyContext.Create(path);
				db.Seed(w.Interactions, ages);
				db.ChangeTracker.AutoDetectChangesEnabled = true;
				var svc = new ApiSchedulingService(db, NullLogger<ApiSchedulingService>.Instance);
				var pkgs = db.Packages.AsNoTracking().ToList();

				// warm up
				for (int i = 0; i < 20; i++) svc.GetNextRun(pkgs[i]);

				var perCallMs = new List<double>();
				var sw = new Stopwatch();
				for (int i = 0; i < sampleCalls; i++)
				{
					var p = pkgs[rng.Next(n)];
					sw.Restart();
					svc.GetNextRun(p);
					perCallMs.Add(sw.Elapsed.TotalMilliseconds);
				}
				perCallMs.Sort();
				double mean = perCallMs.Average();
				double fullPassS = mean * n / 1000.0;

				sw.Restart();
				var tiers = SinglePass(db, pkgs);
				double singleMs = sw.Elapsed.TotalMilliseconds;

				rows.Add([
					n.ToString("N0"),
					mean.ToString("0.00"),
					Stats.Percentile(perCallMs, 0.99).ToString("0.00"),
					fullPassS.ToString("0.0"),
					singleMs.ToString("0.0"),
					(fullPassS * 1000.0 / singleMs).ToString("N0") + "×",
				]);
				data.Add(new { packages = n, perCallMeanMs = mean, perCallP99Ms = Stats.Percentile(perCallMs, 0.99), fullPassSeconds = fullPassS, singlePassMs = singleMs });
				GC.KeepAlive(tiers);
			}
			finally
			{
				SqliteTidy(path);
			}
		}

		rep.Table(["Packages", "GetNextRun mean (ms)", "p99 (ms)", "Full pass, current (s)", "Full pass, single query (ms)", "Ratio"], rows);

		if (data.Count >= 2)
		{
			dynamic a = data[0], b = data[^1];
			double exp = Math.Log((double)b.fullPassSeconds / (double)a.fullPassSeconds) / Math.Log((double)b.packages / (double)a.packages);
			rep.Line($"Full-pass time scales as ~N^{exp:0.00} across this range (2.0 would be quadratic).");
			rep.Line();
		}

		rep.Data = new { env = EnvInfo.Describe(), engine = "sqlite", results = data };
		return rep;
	}

	// Reference implementation of the same percentile semantics in one query.
	private static TimeSpan[] SinglePass(SqliteWheyContext db, List<Whey.Core.Models.Package> pkgs)
	{
		var stats = db.PackageStats.AsNoTracking().Select(s => new { s.Id, s.TotalInteractions }).ToList();
		var sorted = stats.Select(s => s.TotalInteractions).OrderBy(x => x).ToArray();
		var byId = stats.ToDictionary(s => s.Id, s => s.TotalInteractions);
		var res = new TimeSpan[pkgs.Count];
		for (int i = 0; i < pkgs.Count; i++)
		{
			long t = byId[pkgs[i].Id];
			int lo = 0, hi = sorted.Length;
			while (lo < hi) { int mid = (lo + hi) >>> 1; if (sorted[mid] <= t) lo = mid + 1; else hi = mid; }
			double percentile = 1.0 - (double)(sorted.Length - lo) / sorted.Length;
			double age = (DateTimeOffset.UtcNow - pkgs[i].LastReleased!.Value).TotalDays;
			res[i] = ApiSchedulingService.SelectInterval(percentile, age);
		}
		return res;
	}

	private static void SqliteTidy(string path)
	{
		Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
		foreach (var f in Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + "*")) File.Delete(f);
	}
}
