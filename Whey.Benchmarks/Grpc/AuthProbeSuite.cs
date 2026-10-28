using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Whey.Benchmarks.Reporting;
using Whey.Infra.Data;
using Whey.Server.Interceptors;
using Whey.Server.Proto;

namespace Whey.Benchmarks.Grpc;

// Pass/fail probes of registration and proof-of-possession auth against the real server pipeline.
// A security control is only as good as the attacks it rejects, so the metric here is rejection rate.
public static class AuthProbeSuite
{
	public static async Task<SuiteReport> Run()
	{
		var rep = new SuiteReport { Name = "auth-probes" };
		rep.Line("## Auth and registration probes");
		rep.Line();
		var rows = new List<string[]>();
		var data = new List<object>();
		int pass = 0;
		void Add(string probe, string expected, string actual, bool ok)
		{
			if (ok) pass++;
			rows.Add([probe, expected, actual.Length > 90 ? actual[..90] + "…" : actual, ok ? "pass" : "**FAIL**"]);
			data.Add(new { probe, expected, actual, ok });
		}

		using var server = new BenchServer();
		server.EnsureDatabase();
		using var ch = server.CreateChannel();
		var reg = new RegistrationService.RegistrationServiceClient(ch);
		var trk = new PackageTrackerService.PackageTrackerServiceClient(ch);
		var req = new ReportInstallRequest { Owner = "o", Repo = "r", Platform = "linux" };
		var other = new ReportInstallRequest { Owner = "o", Repo = "other", Platform = "linux" };
		const string M = PopClient.ReportInstallMethod;

		async Task<string> Call(ReportInstallRequest r, Metadata md)
		{
			try { await trk.ReportInstallAsync(r, md); return "OK"; }
			catch (RpcException e) { return $"{e.StatusCode}: {e.Status.Detail}"; }
		}
		void Expect(string probe, string result, string expectedCode) =>
			Add(probe, expectedCode, result, expectedCode == "OK" ? result == "OK" : result.StartsWith(expectedCode));

		// Registration
		var a = new PopClient(); var b = new PopClient();
		string ra = "OK", rb = "OK", rdup = "OK";
		try { await a.RegisterAsync(reg); } catch (RpcException e) { ra = $"{e.StatusCode}: {e.Status.Detail}"; }
		try { await b.RegisterAsync(reg); } catch (RpcException e) { rb = $"{e.StatusCode}: {e.Status.Detail}"; }
		Expect("Register first client", ra, "OK");
		Expect("Register a second, distinct client", rb, "OK");
		try { await a.RegisterAsync(reg); } catch (RpcException e) { rdup = $"{e.StatusCode}: {e.Status.Detail}"; }
		Expect("Re-register an existing key", rdup, "AlreadyExists");
		using (var scope = server.Services.CreateScope())
		{
			var keys = scope.ServiceProvider.GetRequiredService<WheyContext>().Clients.Select(c => c.PublicKey).ToList();
			Add("Stored key is the client's public key", AuthenticationInterceptor.EncodePublicKey(a.PublicKey), string.Join(",", keys.Select(k => k[..8])),
				keys.Contains(AuthenticationInterceptor.EncodePublicKey(a.PublicKey)) && keys.Count == 2);
		}
		server.SeedClients([]); // package "o/r" for ReportInstall

		// Honest use
		Expect("Correctly signed request from a registered client", await Call(req, a.Sign(M, req)), "OK");

		using (var scope = server.Services.CreateScope())
		{
			long n = scope.ServiceProvider.GetRequiredService<WheyContext>().PackageStats.Select(x => x.TotalInteractions).Single();
			Add("ReportInstall increments the package's TotalInteractions (scheduler input)", "1", n.ToString(), n == 1);
		}
		var unknown = new ReportInstallRequest { Owner = "nobody", Repo = "nothing", Platform = "linux" };
		Expect("ReportInstall for an untracked package", await Call(unknown, a.Sign(M, unknown)), "NotFound");

		// Attacks. Each must be rejected.
		Expect("No credentials", await Call(req, []), "Unauthenticated");
		Expect("Signed body swapped after signing (tampered payload)", await Call(other, a.Sign(M, other, signedBody: req)), "Unauthenticated");
		Expect("Signature made by a different key", await Call(req, Swap(a, b.Sign(M, req))), "Unauthenticated");
		Expect("Signature for a different method", await Call(req, a.Sign("/package_tracker.v1.PackageTrackerService/EnsureTracked", req)), "Unauthenticated");
		Expect("Valid key + signature, unregistered client", await Call(req, new PopClient().Sign(M, req)), "Unauthenticated");
		Expect("Timestamp 10 minutes old (stale)", await Call(req, a.Sign(M, req, unixSeconds: DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds())), "Unauthenticated");
		Expect("Timestamp 10 minutes ahead", await Call(req, a.Sign(M, req, unixSeconds: DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds())), "Unauthenticated");
		var replay = a.Sign(M, req, nonce: "replay-me");
		Expect("Nonce used for the first time", await Call(req, replay), "OK");
		Expect("Exact replay of that request", await Call(req, a.Sign(M, req, nonce: "replay-me", unixSeconds: long.Parse(replay.GetValue(AuthenticationInterceptor.TimestampHeader)!))), "Unauthenticated");
		Expect("Garbage public key / signature bytes", await Call(req, new Metadata
		{
			{ AuthenticationInterceptor.PublicKeyHeader, new byte[] { 1, 2, 3 } }, { AuthenticationInterceptor.SignatureHeader, new byte[64] },
			{ AuthenticationInterceptor.NonceHeader, "n" }, { AuthenticationInterceptor.TimestampHeader, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() },
		}), "Unauthenticated");

		int attacks = rows.Skip(7).Count(r => r[1] == "Unauthenticated");
		int rejected = rows.Skip(7).Count(r => r[1] == "Unauthenticated" && r[3] == "pass");
		rep.Table(["Probe", "Expected", "Actual", ""], rows);
		rep.Line($"**{pass}/{rows.Count} probes pass; {rejected}/{attacks} attack requests rejected.**");
		rep.Line();
		rep.Data = new { env = EnvInfo.Describe(), probes = data, attacksRejected = rejected, attacks };
		return rep;

		// valid signature metadata from b, but claiming to be a
		Metadata Swap(PopClient claimant, Metadata signedByOther)
		{
			var md = new Metadata();
			foreach (var e in signedByOther)
			{
				if (e.Key == AuthenticationInterceptor.PublicKeyHeader) md.Add(e.Key, claimant.PublicKey);
				else if (e.IsBinary) md.Add(e.Key, e.ValueBytes);
				else md.Add(e.Key, e.Value);
			}
			return md;
		}
	}
}
