using System.Text;
using System.Text.Json;

namespace Whey.Benchmarks.Reporting;

// One suite's output: machine-readable JSON plus a markdown fragment for docs/BENCHMARKS.md.
public sealed class SuiteReport
{
	private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

	public required string Name { get; init; }
	public object? Data { get; set; }
	public StringBuilder Markdown { get; } = new();

	public void Line(string s = "") => Markdown.AppendLine(s);

	public void Table(string[] headers, IEnumerable<string[]> rows)
	{
		Line("| " + string.Join(" | ", headers) + " |");
		Line("|" + string.Join("|", headers.Select(_ => "---")) + "|");
		foreach (var r in rows)
		{
			Line("| " + string.Join(" | ", r) + " |");
		}
		Line();
	}

	public void Save(string outDir)
	{
		Directory.CreateDirectory(outDir);
		File.WriteAllText(Path.Combine(outDir, $"{Name}.json"), JsonSerializer.Serialize(Data, JsonOpts));
		File.WriteAllText(Path.Combine(outDir, $"{Name}.md"), Markdown.ToString());
	}
}

public static class Stats
{
	public static double Percentile(IReadOnlyList<double> sorted, double p)
	{
		if (sorted.Count == 0) return double.NaN;
		double idx = p * (sorted.Count - 1);
		int lo = (int)Math.Floor(idx), hi = (int)Math.Ceiling(idx);
		return sorted[lo] + (sorted[hi] - sorted[lo]) * (idx - lo);
	}

	public static (double mean, double std) MeanStd(IEnumerable<double> xs)
	{
		var a = xs.ToArray();
		if (a.Length == 0) return (double.NaN, double.NaN);
		double m = a.Average();
		double v = a.Length > 1 ? a.Sum(x => (x - m) * (x - m)) / (a.Length - 1) : 0;
		return (m, Math.Sqrt(v));
	}
}

public static class EnvInfo
{
	public static object Describe() => new
	{
		os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
		runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
		cores = Environment.ProcessorCount,
		date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
	};
}
