using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Octokit;
using Quartz;
using Whey.Core.Models.Stats;
using Whey.Infra.Data;
using Whey.Infra.Services;
using Whey.Server.Converters;
using Whey.Server.Proto;

namespace Whey.Server.Grpc;

using WheyPackage = Core.Models.Package;

public class PackageTrackerServiceImpl : PackageTrackerService.PackageTrackerServiceBase
{
	private readonly WheyContext _db;
	private readonly IGitHubClient _client;
	private readonly ISchedulerFactory _schedulerFactory;
	private readonly IApiSchedulingService _schedulingService;

	public PackageTrackerServiceImpl(
		WheyContext db,
		IGitHubClient client,
		ISchedulerFactory schedulerFactory,
		IApiSchedulingService schedulingService)
	{
		_db = db;
		_client = client;
		_schedulerFactory = schedulerFactory;
		_schedulingService = schedulingService;
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
				return new EnsureTrackedResponse();
			}

			var (job, trigger) = _schedulingService.GetNextScheduledJob(package, startAt: DateTimeOffset.UtcNow);
			var scheduler = await _schedulerFactory.GetScheduler();
			await scheduler.ScheduleJob(job, trigger);
		}

		return new EnsureTrackedResponse();
	}

	// TODO: report updates

	public override async Task<ReportInstallResponse> ReportInstall(ReportInstallRequest request, ServerCallContext context)
	{
		var pkgId = await _db.Packages
			.AsNoTracking()
			.Where(p => p.Owner == request.Owner && p.Repo == request.Repo)
			.Select(p => (Guid?)p.Id)
			.FirstOrDefaultAsync(context.CancellationToken);

		if (pkgId is null)
		{
			throw new RpcException(new global::Grpc.Core.Status(StatusCode.NotFound, "Package is not tracked."));
		}

		// The counter drives sync scheduling, so bump it atomically; concurrent reports must not lose updates.
		await _db.PackageStats
			.Where(s => s.PackageId == pkgId)
			.ExecuteUpdateAsync(u => u.SetProperty(s => s.TotalInteractions, s => s.TotalInteractions + 1), context.CancellationToken);

		// The hourly history is best-effort detail; it does not touch TotalInteractions.
		var stats = await _db.PackageStats.FirstOrDefaultAsync(s => s.PackageId == pkgId, context.CancellationToken);
		if (stats is not null)
		{
			stats.Installs.Track();
			await _db.SaveChangesAsync(context.CancellationToken);
		}

		return new ReportInstallResponse();
	}
}
