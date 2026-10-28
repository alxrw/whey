using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using NSec.Cryptography;
using Octokit;
using Whey.Benchmarks.Reporting;
using Whey.Core.Models;
using Whey.Infra.Services;
using Whey.Infra.Utils;

namespace Whey.Benchmarks;

// Hot-path CPU and allocation costs. These are the per-request / per-asset floors that the
// system-level suites sit on top of.
[MemoryDiagnoser]
public class MicroBenchmarks
{
	private Key _key = null!;
	private byte[] _pub = null!, _msg = null!, _sig = null!, _payload = null!;
	private NSec.Cryptography.PublicKey _imported = null!;
	private ReleaseAsset[] _assets5 = null!, _assets40 = null!;
	private readonly AssetMappingService _mapper = new();
	private string _objdump = null!, _elfPath = null!, _textPath = null!;

	[GlobalSetup]
	public void Setup()
	{
		_key = Key.Create(SignatureAlgorithm.Ed25519, new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
		_pub = _key.Export(KeyBlobFormat.RawPublicKey);
		_payload = new byte[200]; // typical small request message
		_msg = Encoding.UTF8.GetBytes($"/package_tracker.v1.PackageTrackerService/ReportInstall|{Guid.NewGuid():N}|1760000000|System.Byte[]");
		_sig = SignatureAlgorithm.Ed25519.Sign(_key, _msg);
		_imported = NSec.Cryptography.PublicKey.Import(SignatureAlgorithm.Ed25519, _pub, KeyBlobFormat.RawPublicKey);

		_assets5 = Assets("tool", ["linux-amd64.tar.gz", "linux-arm64.tar.gz", "darwin-amd64.tar.gz", "darwin-arm64.tar.gz", "windows-amd64.zip"]);
		var big = new List<string>();
		foreach (var os in new[] { "linux", "darwin", "windows", "freebsd" })
			foreach (var arch in new[] { "amd64", "arm64", "386", "armv7" })
				foreach (var ext in new[] { ".tar.gz", ".zip" })
				{ big.Add($"{os}-{arch}{ext}"); big.Add($"{os}-{arch}-musl{ext}"); }
		big.AddRange(["checksums.txt", "checksums.txt.sig", "tool.1.gz", "source.tar.gz", "sbom.json"]);
		_assets40 = Assets("tool", big.Take(40).ToList());

		var sb = new StringBuilder("\n/usr/bin/ls:     file format elf64-x86-64\n\nProgram Header:\n");
		for (int i = 0; i < 14; i++) sb.AppendLine($"    LOAD off    0x{i * 4096:x16} vaddr 0x{i * 4096:x16} paddr 0x{i * 4096:x16} align 2**12");
		sb.AppendLine("\nDynamic Section:");
		foreach (var lib in new[] { "libselinux.so.1", "libc.so.6", "libpcre2-8.so.0", "libm.so.6", "libz.so.1" }) sb.AppendLine($"  NEEDED               {lib}");
		for (int i = 0; i < 20; i++) sb.AppendLine($"  INIT_ARRAY{i}          0x0000000000{i:x4}");
		_objdump = sb.ToString();

		_elfPath = Path.GetTempFileName();
		File.WriteAllBytes(_elfPath, [0x7F, 0x45, 0x4C, 0x46, 2, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
		_textPath = Path.GetTempFileName();
		File.WriteAllText(_textPath, "plain text file contents");
	}

	[GlobalCleanup]
	public void Cleanup() { File.Delete(_elfPath); File.Delete(_textPath); }

	private static ReleaseAsset[] Assets(string name, IList<string> suffixes) =>
		suffixes.Select((s, i) => new ReleaseAsset("", i, "", $"{name}-{s}", "", "uploaded", "application/octet-stream", 1024, 0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "", null)).ToArray();

	[Benchmark(Description = "Ed25519 verify (per authenticated request)")]
	public bool Ed25519Verify() => SignatureAlgorithm.Ed25519.Verify(_imported, _msg, _sig);

	[Benchmark(Description = "Ed25519 public-key import + verify (as the interceptor does)")]
	public bool Ed25519ImportAndVerify()
	{
		var k = NSec.Cryptography.PublicKey.Import(SignatureAlgorithm.Ed25519, _pub, KeyBlobFormat.RawPublicKey);
		return SignatureAlgorithm.Ed25519.Verify(k, _msg, _sig);
	}

	[Benchmark(Description = "Ed25519 sign (client side)")]
	public byte[] Ed25519Sign() => SignatureAlgorithm.Ed25519.Sign(_key, _msg);

	[Benchmark(Description = "SHA-256 of 200 B request body")]
	public byte[] Sha256() => SHA256.HashData(_payload);

	[Benchmark(Description = "SelectReleaseAsset, 5-asset release")]
	public int SelectAssets5() => _mapper.SelectReleaseAsset(_assets5, Platform.Linux, ProcessorArchitecture.Amd64, strictMatchesOnly: true).Count();

	[Benchmark(Description = "SelectReleaseAsset, 40-asset release")]
	public int SelectAssets40() => _mapper.SelectReleaseAsset(_assets40, Platform.Linux, ProcessorArchitecture.Amd64, strictMatchesOnly: true).Count();

	[Benchmark(Description = "Parse objdump -p output (NEEDED regex)")]
	public int ParseObjdump() => DependencyFinderService.ParseObjdumpOutput(_objdump).Length;

	[Benchmark(Description = "Classify one file (4-byte magic read)")]
	public BinaryType Classify() => BinaryInspector.GetBinaryExecutableType(_elfPath);
}

public static class MicroSuite
{
	public static SuiteReport Run(bool quick, string outDir)
	{
		var job = (quick ? Job.ShortRun : Job.Default).WithToolchain(InProcessEmitToolchain.Instance);
		var cfg = ManualConfig.CreateEmpty()
			.AddJob(job)
			.AddLogger(BenchmarkDotNet.Loggers.ConsoleLogger.Unicode)
			.WithOptions(ConfigOptions.DisableOptimizationsValidator)
			.WithArtifactsPath(Path.Combine(Path.GetTempPath(), "whey-bdn"));
		var summary = BenchmarkRunner.Run<MicroBenchmarks>(cfg);

		var rep = new SuiteReport { Name = "micro" };
		rep.Line("## Micro-benchmarks (BenchmarkDotNet)");
		rep.Line();
		var rows = new List<string[]>();
		var data = new List<object>();
		foreach (var r in summary.Reports.OrderBy(r => r.BenchmarkCase.DisplayInfo))
		{
			var st = r.ResultStatistics;
			if (st is null) continue;
			double allocB = r.GcStats.GetBytesAllocatedPerOperation(r.BenchmarkCase) ?? 0;
			string Fmt(double ns) => ns >= 1000 ? $"{ns / 1000:0.00} µs" : $"{ns:0.0} ns";
			rows.Add([r.BenchmarkCase.Descriptor.WorkloadMethodDisplayInfo.Trim('\''), Fmt(st.Mean), Fmt(st.StandardDeviation), $"{1e9 / st.Mean:N0}", $"{allocB:N0} B"]);
			data.Add(new { name = r.BenchmarkCase.Descriptor.WorkloadMethodDisplayInfo.Trim('\''), meanNs = st.Mean, stdNs = st.StandardDeviation, opsPerSec = 1e9 / st.Mean, allocBytes = allocB });
		}
		rep.Table(["Benchmark", "Mean", "Std dev", "Ops/s (1 core)", "Allocated/op"], rows);
		rep.Data = new { env = EnvInfo.Describe(), results = data };
		return rep;
	}
}
