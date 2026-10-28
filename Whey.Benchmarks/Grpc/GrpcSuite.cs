using System.Diagnostics;
using Grpc.Core;
using Whey.Benchmarks.Reporting;
using Whey.Server.Proto;

namespace Whey.Benchmarks.Grpc;

// Questions answered:
//  1. What does the gRPC pipeline (interceptors, rate limiter, handler, EF) cost per request and where does it saturate?
//  2. Does the per-key limiter admit what it is configured to (token bucket: burst 40, +5/s, queue 10)?
//  3. Does one key flooding the server hurt other keys (isolation)?
// Every call is a real signed request: Ed25519 signature, nonce and timestamp are verified and recorded.
public static class GrpcSuite
{
	private const int Burst = 40, RefillPerSec = 5, QueueLimit = 10; // from Program.cs

	public static async Task<SuiteReport> Run(bool quick)
	{
		var rep = new SuiteReport { Name = "grpc" };
		rep.Line("## gRPC pipeline and rate limiting");
		rep.Line();
		rep.Line("Real `Whey.Server` pipeline on an in-process TestServer + SQLite (no network/TLS/Postgres). " +
			"Every authenticated call carries a real Ed25519 signature, nonce and timestamp, so the numbers include authentication.");
		rep.Line();

		using var server = new BenchServer();
		server.EnsureDatabase();
		// Pool of distinct keys so throughput runs never trip the per-key limiter (burst 40).
		var pool = Enumerable.Range(0, quick ? 1_000 : 5_000).Select(_ => new PopClient()).ToArray();
		var flooder = new PopClient(); var victimAlone = new PopClient(); var victimContended = new PopClient(); var abuser = new PopClient();
		server.SeedClients(pool.Concat([flooder, victimAlone, victimContended, abuser]));
		using var channel = server.CreateChannel();
		var tracker = new PackageTrackerService.PackageTrackerServiceClient(channel);
		var registration = new RegistrationService.RegistrationServiceClient(channel);

		double seconds = quick ? 2 : 6;
		var tp = await Throughput(tracker, registration, pool, seconds, rep);
		var acc = await RateLimitAccuracy(tracker, flooder, quick ? 6 : 15, rep);
		var iso = await Isolation(tracker, victimAlone, victimContended, abuser, quick ? 6 : 15, rep);

		rep.Data = new { env = EnvInfo.Describe(), throughput = tp, rateLimit = acc, isolation = iso };
		return rep;
	}

	private static Metadata Auth(PopClient c, ReportInstallRequest r) => c.Sign(PopClient.ReportInstallMethod, r);

	private sealed record Sample(double Ms, StatusCode Code);

	private static async Task<List<Sample>> ClosedLoop(int concurrency, TimeSpan duration, Func<int, long, Task> call, Func<long, string>? _ = null)
	{
		var all = new List<Sample>[concurrency];
		using var cts = new CancellationTokenSource(duration);
		long seq = 0;
		var workers = Enumerable.Range(0, concurrency).Select(w => Task.Run(async () =>
		{
			var list = new List<Sample>();
			var sw = new Stopwatch();
			while (!cts.IsCancellationRequested)
			{
				long n = Interlocked.Increment(ref seq);
				sw.Restart();
				StatusCode code = StatusCode.OK;
				try { await call(w, n); }
				catch (RpcException e) { code = e.StatusCode; }
				list.Add(new Sample(sw.Elapsed.TotalMilliseconds, code));
			}
			all[w] = list;
		})).ToArray();
		await Task.WhenAll(workers);
		return all.SelectMany(x => x).ToList();
	}

	private static async Task<object> Throughput(PackageTrackerService.PackageTrackerServiceClient trk, RegistrationService.RegistrationServiceClient reg, PopClient[] pool, double seconds, SuiteReport rep)
	{
		rep.Line("### Pipeline latency and throughput");
		rep.Line();
			var req = new ReportInstallRequest { Owner = "o", Repo = "r", Platform = "linux" };
		var rows = new List<string[]>();
		var data = new List<object>();
		foreach (var (name, call) in new (string, Func<int, long, Task>)[]
		{
			("ReportInstall (auth + limiter + handler + EF)", async (_, n) => await trk.ReportInstallAsync(req, Auth(pool[n % pool.Length], req))),
			("Challenge (public path: skips auth + limiter)", async (_, _) => await reg.ChallengeAsync(new ChallengeRequest())),
		})
		{
			foreach (int c in new[] { 1, 16, 64 })
			{
				await ClosedLoop(c, TimeSpan.FromMilliseconds(500), call); // warm-up
				var res = await ClosedLoop(c, TimeSpan.FromSeconds(seconds), call);
				var ok = res.Where(s => s.Code == StatusCode.OK).Select(s => s.Ms).Order().ToList();
				double rps = ok.Count / seconds;
				rows.Add([name, c.ToString(), rps.ToString("N0"), Stats.Percentile(ok, 0.5).ToString("0.00"), Stats.Percentile(ok, 0.95).ToString("0.00"),
					Stats.Percentile(ok, 0.99).ToString("0.00"), (res.Count - ok.Count).ToString()]);
				data.Add(new { rpc = name, concurrency = c, rps, p50 = Stats.Percentile(ok, 0.5), p95 = Stats.Percentile(ok, 0.95), p99 = Stats.Percentile(ok, 0.99), errors = res.Count - ok.Count });
			}
		}
		rep.Table(["RPC", "Concurrency", "Req/s", "p50 (ms)", "p95 (ms)", "p99 (ms)", "Errors"], rows);
		return data;
	}

	private static async Task<object> RateLimitAccuracy(PackageTrackerService.PackageTrackerServiceClient trk, PopClient flooder, double seconds, SuiteReport rep)
	{
		rep.Line("### Rate-limit accuracy (one key, 32 requests always in flight)");
		rep.Line();
		var req = new ReportInstallRequest { Owner = "o", Repo = "r", Platform = "linux" };
		var sw = Stopwatch.StartNew();
		var res = await ClosedLoop(32, TimeSpan.FromSeconds(seconds), (_, _) => trk.ReportInstallAsync(req, Auth(flooder, req)).ResponseAsync);
		double elapsed = sw.Elapsed.TotalSeconds;
		int ok = res.Count(s => s.Code == StatusCode.OK);
		int limited = res.Count(s => s.Code == StatusCode.ResourceExhausted);
		int other = res.Count - ok - limited;
		// Expected admissions: initial burst, plus refill over the run, plus up to QueueLimit requests parked at the end.
		double expectedLo = Burst + RefillPerSec * (elapsed - 1);
		double expectedHi = Burst + RefillPerSec * elapsed + QueueLimit;
		var okLat = res.Where(s => s.Code == StatusCode.OK).Select(s => s.Ms).Order().ToList();
		var rejLat = res.Where(s => s.Code == StatusCode.ResourceExhausted).Select(s => s.Ms).Order().ToList();

		rep.Table(["Run (s)", "Offered req/s", "Admitted", "Expected admitted", "Steady admitted req/s (configured 5)", "Rejected (RESOURCE_EXHAUSTED)", "Other errors", "Rejection latency p50 (ms)"],
		[[
			elapsed.ToString("0.0"), (res.Count / elapsed).ToString("N0"), ok.ToString(), $"{expectedLo:0}–{expectedHi:0}",
			((ok - Burst) / elapsed).ToString("0.00"), limited.ToString("N0"), other.ToString(), Stats.Percentile(rejLat, 0.5).ToString("0.00"),
		]]);
		rep.Line($"Admitted-request latency p50 {Stats.Percentile(okLat, 0.5):0} ms / p99 {Stats.Percentile(okLat, 0.99):0} ms (queued requests wait for tokens).");
		rep.Line();
		return new { elapsed, offeredRps = res.Count / elapsed, admitted = ok, rejected = limited, other, expectedLo, expectedHi, admittedP50 = Stats.Percentile(okLat, 0.5), rejectP50 = Stats.Percentile(rejLat, 0.5) };
	}

	private static async Task<object> Isolation(PackageTrackerService.PackageTrackerServiceClient trk, PopClient victimAlone, PopClient victimContended, PopClient abuser, double seconds, SuiteReport rep)
	{
		rep.Line("### Isolation: a well-behaved key while another key floods");
		rep.Line();
		var req = new ReportInstallRequest { Owner = "o", Repo = "r", Platform = "linux" };

		async Task<List<Sample>> Victim(PopClient who, TimeSpan d)
		{
			var list = new List<Sample>();
			using var cts = new CancellationTokenSource(d);
			var sw = new Stopwatch();
			while (!cts.IsCancellationRequested)
			{
				sw.Restart();
				StatusCode code = StatusCode.OK;
				try { await trk.ReportInstallAsync(req, Auth(who, req)); } catch (RpcException e) { code = e.StatusCode; }
				list.Add(new Sample(sw.Elapsed.TotalMilliseconds, code));
				try { await Task.Delay(250, cts.Token); } catch (OperationCanceledException) { }
			}
			return list;
		}

		var baseline = await Victim(victimAlone, TimeSpan.FromSeconds(seconds));

		var flood = ClosedLoop(64, TimeSpan.FromSeconds(seconds), (_, _) => trk.ReportInstallAsync(req, Auth(abuser, req)).ResponseAsync);
		var contended = await Victim(victimContended, TimeSpan.FromSeconds(seconds));
		var floodRes = await flood;

		string Row(string label, List<Sample> s)
		{
			var ok = s.Where(x => x.Code == StatusCode.OK).Select(x => x.Ms).Order().ToList();
			return $"| {label} | {s.Count} | {100.0 * ok.Count / s.Count:0.#}% | {Stats.Percentile(ok, 0.5):0.00} | {Stats.Percentile(ok, 0.99):0.00} |";
		}
		rep.Line("| Victim (4 req/s) | Requests | Success | p50 (ms) | p99 (ms) |");
		rep.Line("|---|---|---|---|---|");
		rep.Line(Row("alone", baseline));
		rep.Line(Row($"while abuser sends {floodRes.Count / seconds:N0} req/s", contended));
		rep.Line();
		rep.Line($"Abuser: {floodRes.Count(s => s.Code == StatusCode.OK)} admitted, {floodRes.Count(s => s.Code == StatusCode.ResourceExhausted):N0} rejected.");
		rep.Line();

		double Succ(List<Sample> s) => 100.0 * s.Count(x => x.Code == StatusCode.OK) / s.Count;
		double P99(List<Sample> s) => Stats.Percentile(s.Where(x => x.Code == StatusCode.OK).Select(x => x.Ms).Order().ToList(), 0.99);
		return new { baselineSuccess = Succ(baseline), contendedSuccess = Succ(contended), baselineP99 = P99(baseline), contendedP99 = P99(contended), abuserRps = floodRes.Count / seconds };
	}
}
