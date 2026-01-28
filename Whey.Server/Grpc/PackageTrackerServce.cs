using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Octokit;
using Whey.Core.Models.Stats;
using Whey.Infra.Data;
using Whey.Server.Converters;
using Whey.Server.Proto;

namespace Whey.Server.Grpc;

using WheyPackage = Core.Models.Package;

[Authorize]
public class PackageTrackerServiceImpl : PackageTrackerService.PackageTrackerServiceBase
{
	private readonly WheyContext _db;
	private readonly IGitHubClient _client;

	public PackageTrackerServiceImpl(WheyContext db, IGitHubClient client)
	{
		_db = db;
		_client = client;
	}

	public override async Task<EnsureTrackedResponse> EnsureTracked(EnsureTrackedRequest request, ServerCallContext context)
	{
		Release rel = await _client.Repository.Release.GetLatest(request.Owner, request.Repo);
		WheyPackage package = new()
		{
			Owner = request.Owner,
			Repo = request.Repo,
			Version = rel.TagName,
			LastReleased = rel.PublishedAt,
			SupportedPlatforms = PlatformConverter.ConvertStringToCore(request.Platform),
		};
		bool exists = await _db.Packages
			.AnyAsync(p => p.Owner == request.Owner && p.Repo == request.Repo);

		if (!exists)
		{
			_db.Packages.Add(package);
			package.Id = Guid.CreateVersion7();

			PackageStatistics stats = new()
			{
				Id = package.Id, // change? maybe just generate a long?
				PackageId = package.Id,
				Installs = new(),
				Updates = new(),
			};
			stats.Installs.Track();
			_db.PackageStats.Add(stats);

			// TODO: put this elsewhere, maybe batch saves?
			try
			{
				await _db.SaveChangesAsync();
			}
			catch (DbUpdateException)
			{
				// NO-OP
			}
		}

		return new EnsureTrackedResponse();
	}

	// TODO: report updates

	public override async Task<ReportInstallResponse> ReportInstall(ReportInstallRequest request, ServerCallContext context)
	{
		var pkg = await _db.Packages
			.FirstOrDefaultAsync(p => p.Owner == request.Owner && p.Repo == request.Repo);

		// TODO: do something, increment download metrics

		return new ReportInstallResponse();
	}
}
