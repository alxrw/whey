using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Octokit;
using Whey.Core.Models;
using Whey.Infra.Data;
using Whey.Server.Converters;
using Whey.Server.Proto;

namespace Whey.Server.Grpc;

using GhPackage = Octokit.Package;
using WheyPackage = Whey.Core.Models.Package;

[Authorize]
public class PackageTrackerServiceImpl : PackageTrackerService.PackageTrackerServiceBase
{
	private readonly WheyContext _db;
	private readonly GitHubClient _client;

	public PackageTrackerServiceImpl(WheyContext db, GitHubClient client)
	{
		_db = db;
		_client = client;
	}

	public override async Task<TrackResponse> Track(TrackRequest request, ServerCallContext context)
	{
		Release rel = await _client.Repository.Release.GetLatest(request.Owner, request.Repo);
		WheyPackage package = new WheyPackage
		{
			Owner = request.Owner,
			Repo = request.Repo,
			Version = rel.TagName,
			SupportedPlatforms = PlatformConverter.ConvertStringToCore(request.Platform),
		};
		await _db.Packages.AddAsync(package);
		return null!;
	}

	public override async Task<BumpResponse> Bump(BumpRequest request, ServerCallContext context)
	{
		var pkg = await _db.Packages
			.FirstOrDefaultAsync(p => p.Owner == request.Owner && p.Repo == request.Repo);
		return null!;
	}
}
