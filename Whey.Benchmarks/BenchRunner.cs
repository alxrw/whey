using Whey.Benchmarks.Reporting;
using Whey.Benchmarks.Scheduling;

namespace Whey.Benchmarks;

public static class BenchRunner
{
	private static readonly string[] Suites = ["sim", "sched-cost", "binary", "auth-probes", "grpc", "micro"];

	public static async Task<int> Main(string[] args)
	{
		if (args.Length == 0 || args[0] is "-h" or "--help")
		{
			Console.WriteLine($"usage: Whey.Benchmarks <{string.Join("|", Suites)}|all> [--quick] [--out <dir>]");
			return 1;
		}

		if (args[0] == "fd-probe")
		{
			try { Console.WriteLine($"ok, {Whey.Infra.Utils.BinaryInspector.FindBinaries(args[1]).Count} binaries found"); return 0; }
			catch (Exception ex) { Console.WriteLine($"FAILED with {ex.GetType().Name}: {ex.Message}"); return 2; }
		}

		bool quick = args.Contains("--quick");
		int oi = Array.IndexOf(args, "--out");
		string outDir = oi >= 0 ? args[oi + 1] : Path.Combine(FindRepoRoot(), "bench", "results");

		if (args[0] == "summary") { WriteSummary(outDir, Suites, quick); return 0; }

		var selected = args[0] == "all" ? Suites : [args[0]];
		foreach (var s in selected)
		{
			Console.WriteLine($"=== {s} ===");
			SuiteReport? rep = s switch
			{
				"sim" => SchedulerSimSuite.Run(quick),
				"sched-cost" => SchedulerCostSuite.Run(quick),
				"binary" => Whey.Benchmarks.Binary.BinarySuite.Run(quick),
				"grpc" => await Whey.Benchmarks.Grpc.GrpcSuite.Run(quick),
				"micro" => MicroSuite.Run(quick, outDir),
				"auth-probes" => await Whey.Benchmarks.Grpc.AuthProbeSuite.Run(),
				_ => null,
			};
			if (rep is null)
			{
				Console.WriteLine($"unknown suite {s}");
				return 1;
			}
			rep.Save(outDir);
			Console.WriteLine(rep.Markdown.ToString());
		}
		if (args.Contains("--check") && selected.Contains("sim"))
		{
			// Regression gate for the deterministic suite: the simulator must still match the real scheduler,
			// and tiering must still beat uniform polling at equal budget when interaction data exists.
			var fid = SchedulerSimSuite.Fidelity;
			dynamic mature = SchedulerSimSuite.Summaries[0];
			bool ok = fid.matches == fid.total && (double)mature.staleReductionVsEqualBudget > 0.25;
			Console.WriteLine($"check: fidelity {fid.matches}/{fid.total}, stale-install reduction at equal budget {(double)mature.staleReductionVsEqualBudget:P1} (min 25%): {(ok ? "PASS" : "FAIL")}");
			if (!ok) return 3;
		}

		if (selected.Length > 1) WriteSummary(outDir, selected, quick);
		await Task.CompletedTask;
		return 0;
	}

	private static void WriteSummary(string outDir, string[] suites, bool quick)
	{
		var sb = new System.Text.StringBuilder("# Whey benchmark results\n\n");
		sb.AppendLine($"Generated {DateTimeOffset.UtcNow:yyyy-MM-dd} on {System.Runtime.InteropServices.RuntimeInformation.OSDescription}, {Environment.ProcessorCount} cores, {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}{(quick ? " (--quick: reduced sizes, do not publish)" : "")}.\n");
		foreach (var s in suites) sb.AppendLine(File.ReadAllText(Path.Combine(outDir, (s == "sim" ? "scheduler-sim" : s) + ".md")));
		File.WriteAllText(Path.Combine(outDir, "summary.md"), sb.ToString());
	}

	private static string FindRepoRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Whey.sln"))) dir = dir.Parent;
		return dir?.FullName ?? Directory.GetCurrentDirectory();
	}
}
