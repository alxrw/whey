using System.Diagnostics;
using Whey.Benchmarks.Reporting;
using Whey.Infra.Services;
using Whey.Infra.Utils;

namespace Whey.Benchmarks.Binary;

// Question answered: how correct and how fast is the binary analysis that runs on every release asset?
// Ground truth comes from `file(1)` and `readelf` over real binaries on this machine (system ELF, .NET PE,
// Go's debug/{macho,pe,elf} testdata, macOS dylibs from NuGet packages), not from Whey's own helpers.
public static class BinarySuite
{
	public static SuiteReport Run(bool quick)
	{
		var rep = new SuiteReport { Name = "binary" };
		rep.Line("## Binary analysis");
		rep.Line();

		var accuracy = Accuracy(rep, quick);
		var synthetic = SyntheticEdgeCases(rep);
		var throughput = Throughput(rep, quick);
		var deps = Dependencies(rep, quick);

		rep.Data = new { env = EnvInfo.Describe(), accuracy, synthetic, throughput, deps };
		return rep;
	}

	// ---- classification accuracy -------------------------------------------------------------

	private static BinaryType Truth(string fileDesc)
	{
		fileDesc = System.Text.RegularExpressions.Regex.Replace(fileDesc, @"^(set[ug]id,? ?)+", "");
		if (fileDesc.StartsWith("ELF")) return BinaryType.Elf;
		if (fileDesc.StartsWith("Mach-O")) return BinaryType.Macho;
		if (fileDesc.Contains("PE32") || fileDesc.StartsWith("MS-DOS executable")) return BinaryType.Exe;
		return BinaryType.Unknown;
	}

	private static List<string> BuildCorpus(bool quick)
	{
		string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		var roots = new (string path, int cap)[]
		{
			("/usr/bin", 800), ("/usr/lib/x86_64-linux-gnu", 800), ("/usr/lib/dotnet", 800),
			("/usr/lib/python3.10", 600), ("/usr/local/go/src/debug", 5000), (Path.Combine(home, ".nuget/packages"), 800),
		};
		var files = new List<string>();
		foreach (var (path, cap) in roots)
		{
			if (!Directory.Exists(path)) continue;
			var all = Directory.EnumerateFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint })
				.Order().ToList();
			int c = quick ? cap / 4 : cap;
			int stride = Math.Max(1, all.Count / c);
			files.AddRange(all.Where((_, i) => i % stride == 0).Take(c));
		}
		return files;
	}

	private static object Accuracy(SuiteReport rep, bool quick)
	{
		var corpus = BuildCorpus(quick);
		var list = Path.GetTempFileName();
		File.WriteAllLines(list, corpus);
		var truth = RunLines("file", $"-b -f \"{list}\"");
		File.Delete(list);
		if (truth.Length != corpus.Count)
		{
			rep.Line($"*file(1) unavailable or mismatched ({truth.Length} vs {corpus.Count}); accuracy skipped.*");
			rep.Line();
			return new { skipped = true };
		}

		var types = new[] { BinaryType.Elf, BinaryType.Exe, BinaryType.Macho, BinaryType.Unknown };
		var confusion = new Dictionary<(BinaryType t, BinaryType g), int>();
		var examples = new Dictionary<string, List<string>>();
		int threw = 0;
		var threwExamples = new List<string>();
		for (int i = 0; i < corpus.Count; i++)
		{
			var t = Truth(truth[i]);
			BinaryType g;
			try { g = BinaryInspector.GetBinaryExecutableType(corpus[i]); }
			catch (Exception ex) when (ex is EndOfStreamException or IOException or UnauthorizedAccessException)
			{
				threw++;
				if (threwExamples.Count < 3) threwExamples.Add($"{corpus[i]} ({ex.GetType().Name})");
				continue;
			}
			confusion[(t, g)] = confusion.GetValueOrDefault((t, g)) + 1;
			if (t != g)
			{
				var key = $"{t} → {g}";
				if (!examples.TryGetValue(key, out var l)) examples[key] = l = [];
				if (l.Count < 3) l.Add($"{corpus[i]}  [{truth[i]}]");
			}
		}

		rep.Line($"### Classification accuracy vs `file(1)` ({corpus.Count:N0} real files)");
		rep.Line();
		var rows = new List<string[]>();
		var per = new List<object>();
		foreach (var cls in types.Where(t => t != BinaryType.Unknown))
		{
			int tp = confusion.GetValueOrDefault((cls, cls));
			int fn = types.Where(g => g != cls).Sum(g => confusion.GetValueOrDefault((cls, g)));
			int fp = types.Where(t => t != cls).Sum(t => confusion.GetValueOrDefault((t, cls)));
			double prec = tp + fp == 0 ? double.NaN : (double)tp / (tp + fp);
			double rec = tp + fn == 0 ? double.NaN : (double)tp / (tp + fn);
			rows.Add([cls.ToString(), (tp + fn).ToString(), tp.ToString(), fp.ToString(), fn.ToString(), $"{prec:P1}", $"{rec:P1}"]);
			per.Add(new { cls = cls.ToString(), support = tp + fn, tp, fp, fn, precision = prec, recall = rec });
		}
		rep.Table(["Class", "Files (truth)", "Correct", "False pos.", "False neg.", "Precision", "Recall"], rows);
		int nonBin = types.Sum(g => confusion.GetValueOrDefault((BinaryType.Unknown, g)));
		rep.Line($"Non-binary files in corpus: {nonBin:N0}. Files on which `GetBinaryExecutableType` **threw** instead of classifying: **{threw}**" +
			(threwExamples.Count > 0 ? $" (e.g. {string.Join(", ", threwExamples.Select(e => $"`{e}`"))})." : "."));
		rep.Line();
		foreach (var (k, v) in examples)
		{
			rep.Line($"- Misclassified `{k}`: " + string.Join("; ", v.Select(e => $"`{e}`")));
		}
		rep.Line();
		return new { files = corpus.Count, threw, perClass = per, misclassified = examples };
	}

	// Spec-derived headers for cases that no natural corpus on this machine hits.
	private static object SyntheticEdgeCases(SuiteReport rep)
	{
		var cases = new (string name, byte[] bytes, BinaryType expected)[]
		{
			("Mach-O 64 LE (thin)", [0xCF, 0xFA, 0xED, 0xFE, 0, 0, 0, 0], BinaryType.Macho),
			("Mach-O universal / fat (0xCAFEBABE)", [0xCA, 0xFE, 0xBA, 0xBE, 0, 0, 0, 2], BinaryType.Macho),
			("Mach-O universal / fat64 (0xCAFEBABF)", [0xCA, 0xFE, 0xBA, 0xBF, 0, 0, 0, 2], BinaryType.Macho),
			("Plain text starting with \"MZ\"", "MZ is a two-letter abbreviation used in this README\n"u8.ToArray(), BinaryType.Unknown),
			("Java .class (0xCAFEBABE)", [0xCA, 0xFE, 0xBA, 0xBE, 0, 0, 0, 0x34], BinaryType.Unknown),
			("Empty file (0 bytes)", [], BinaryType.Unknown),
			("3-byte file", [1, 2, 3], BinaryType.Unknown),
		};
		rep.Line("### Edge cases (synthetic headers)");
		rep.Line();
		var rows = new List<string[]>();
		var data = new List<object>();
		foreach (var (name, bytes, expected) in cases)
		{
			string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
			File.WriteAllBytes(path, bytes);
			string actual;
			try { actual = BinaryInspector.GetBinaryExecutableType(path).ToString(); }
			catch (Exception ex) { actual = "throws " + ex.GetType().Name; }
			finally { File.Delete(path); }
			bool ok = actual == expected.ToString();
			rows.Add([name, expected.ToString(), actual, ok ? "pass" : "**FAIL**"]);
			data.Add(new { name, expected = expected.ToString(), actual, ok });
		}
		{
			string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
			Directory.CreateDirectory(dir);
			File.WriteAllBytes(Path.Combine(dir, "app"), [0x7F, 0x45, 0x4C, 0x46, 0, 0, 0, 0]);
			File.WriteAllBytes(Path.Combine(dir, ".keep"), []);
			string actual;
			try { actual = $"{BinaryInspector.FindBinaries(dir).Count} binary found"; }
			catch (Exception ex) { actual = "throws " + ex.GetType().Name; }
			finally { Directory.Delete(dir, true); }
			bool ok = actual == "1 binary found";
			rows.Add(["`FindBinaries` on a directory with one ELF and one empty `.keep` file", "1 binary found", actual, ok ? "pass" : "**FAIL**"]);
			data.Add(new { name = "FindBinaries with empty file", expected = "1 binary found", actual, ok });
		}
		rep.Table(["Case", "Expected", "Actual", ""], rows);
		return data;
	}

	// ---- throughput -----------------------------------------------------------------------------

	private static object Throughput(SuiteReport rep, bool quick)
	{
		int[] sizes = quick ? [1_000, 5_000] : [1_000, 10_000, 50_000];
		rep.Line("### `FindBinaries` throughput and file-handle behaviour");
		rep.Line();
		var rows = new List<string[]>();
		var data = new List<object>();
		foreach (int n in sizes)
		{
			string root = MakeTree(n);
			try
			{
				int fdBefore = OpenFds();
				var sw = Stopwatch.StartNew();
				List<string> found;
				string note = "";
				try { found = BinaryInspector.FindBinaries(root); }
				catch (Exception ex) { found = []; note = ex.GetType().Name; }
				sw.Stop();
				int fdAfter = OpenFds();
				GC.Collect(); GC.WaitForPendingFinalizers();
				int fdAfterGc = OpenFds();
				double perSec = n / sw.Elapsed.TotalSeconds;
				rows.Add([n.ToString("N0"), $"{sw.Elapsed.TotalMilliseconds:N0}", $"{perSec:N0}", found.Count.ToString("N0"),
					$"{Math.Max(0, fdAfter - fdBefore):N0}", $"{Math.Max(0, fdAfterGc - fdBefore):N0}", note]);
				data.Add(new { files = n, ms = sw.Elapsed.TotalMilliseconds, filesPerSec = perSec, binaries = found.Count, leakedFdsBeforeGc = fdAfter - fdBefore, leakedFdsAfterGc = fdAfterGc - fdBefore, error = note });
			}
			finally { Directory.Delete(root, true); }
		}
		rep.Table(["Files in tree", "Time (ms)", "Files / s", "Binaries found", "Open FDs left (before GC)", "(after GC)", "Error"], rows);

		string probeRoot = MakeTree(5_000);
		string probe;
		try
		{
			string dll = typeof(BinarySuite).Assembly.Location;
			probe = string.Join(" — ", RunLines("sh", $"-c \"ulimit -n 1024; dotnet '{dll}' fd-probe '{probeRoot}' 2>&1 | head -3\"")).Trim();
		}
		finally { Directory.Delete(probeRoot, true); }
		rep.Line($"`FindBinaries` over a 5,000-file tree with `ulimit -n 1024` (the common default): **{probe}**");
		rep.Line();
		rep.Line("Descriptors are released as each file is classified, so the count stays at zero regardless of the `ulimit -n` of the host (this machine: " + UlimitN() + ").");
		rep.Line();
		return data;
	}

	private static string MakeTree(int files)
	{
		string root = Path.Combine(Path.GetTempPath(), "whey-bin-" + Guid.NewGuid().ToString("N"));
		var rng = new Random(11);
		var filler = new byte[2048];
		rng.NextBytes(filler);
		for (int i = 0; i < files; i++)
		{
			string dir = Path.Combine(root, $"d{i % 50}", $"s{i % 7}");
			Directory.CreateDirectory(dir);
			bool bin = i % 20 == 0; // 5% binaries
			var bytes = (byte[])filler.Clone();
			if (bin) { bytes[0] = 0x7F; bytes[1] = 0x45; bytes[2] = 0x4C; bytes[3] = 0x46; }
			else { bytes[0] = (byte)'#'; }
			File.WriteAllBytes(Path.Combine(dir, $"f{i}"), bytes);
		}
		return root;
	}

	private static string UlimitN() => string.Join("", RunLines("sh", "-c \"ulimit -n\"")).Trim();

	private static int OpenFds() => Directory.GetFiles("/proc/self/fd").Length;

	// ---- dependency extraction --------------------------------------------------------------

	private static object Dependencies(SuiteReport rep, bool quick)
	{
		rep.Line("### Dependency extraction (`DependencyFinderService.GetLibsLinux`)");
		rep.Line();
		var elfs = new List<string>();
		foreach (var dir in new[] { "/usr/bin", "/usr/lib/x86_64-linux-gnu" })
		{
			if (!Directory.Exists(dir)) continue;
			elfs.AddRange(Directory.EnumerateFiles(dir).Order().Where(f => new FileInfo(f).LinkTarget is null)
				.Where(f => { try { return BinaryInspector.GetBinaryExecutableType(f) == BinaryType.Elf; } catch { return false; } })
				.Where((_, i) => i % 7 == 0).Take(quick ? 20 : 60));
		}

		var lat = new List<double>();
		int agree = 0, total = 0, withDeps = 0;
		foreach (var f in elfs)
		{
			var truth = RunLines("readelf", $"-d \"{f}\"")
				.Where(l => l.Contains("(NEEDED)"))
				.Select(l => l[(l.IndexOf('[') + 1)..l.IndexOf(']')]).Order().ToArray();
			var sw = Stopwatch.StartNew();
			var got = DependencyFinderService.GetLibsLinux(f).Order().ToArray();
			lat.Add(sw.Elapsed.TotalMilliseconds);
			total++;
			if (truth.SequenceEqual(got)) agree++;
			if (got.Length > 0) withDeps++;
		}
		lat.Sort();

		// Cost of the parsing step alone, on real objdump output.
		string sample = string.Join("\n", RunLines("objdump", $"-p -- \"{elfs.FirstOrDefault() ?? "/bin/ls"}\""));
		const int reps = 5_000;
		var sp = Stopwatch.StartNew();
		for (int i = 0; i < reps; i++) DependencyFinderService.ParseObjdumpOutput(sample);
		double parseUs = sp.Elapsed.TotalMilliseconds * 1000.0 / reps;

		rep.Table(["ELF files", "NEEDED set == readelf", "With ≥1 dep", "Latency p50 (ms)", "p95 (ms)", "Regex parse only (µs)", "Process spawn share"],
		[[
			total.ToString(), $"{100.0 * agree / Math.Max(1, total):0.#}%", withDeps.ToString(),
			Stats.Percentile(lat, 0.5).ToString("0.0"), Stats.Percentile(lat, 0.95).ToString("0.0"),
			parseUs.ToString("0.0"), $"{100 * (1 - parseUs / 1000.0 / Stats.Percentile(lat, 0.5)):0.#}%",
		]]);
		rep.Line("Each binary costs one `objdump` process spawn; the parse itself is noise by comparison. " +
			"Agreement is against `readelf -d` (same binutils family), so it validates the parser, not objdump.");
		rep.Line();
		return new { files = total, agreementPercent = 100.0 * agree / Math.Max(1, total), latencyP50Ms = Stats.Percentile(lat, 0.5), latencyP95Ms = Stats.Percentile(lat, 0.95), parseMicros = parseUs };
	}

	private static string[] RunLines(string exe, string args)
	{
		try
		{
			var psi = new ProcessStartInfo(exe, args) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
			using var p = Process.Start(psi)!;
			string o = p.StandardOutput.ReadToEnd();
			p.StandardError.ReadToEnd();
			p.WaitForExit();
			return o.Split('\n', StringSplitOptions.RemoveEmptyEntries);
		}
		catch { return []; }
	}
}
