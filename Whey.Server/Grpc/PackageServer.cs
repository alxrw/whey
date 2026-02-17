using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Whey.Infra.Configuration;
using Whey.Infra.Data;
using Whey.Infra.Services;
using Whey.Server.Converters;
using Whey.Server.Proto;

using ProtoPlatform = Whey.Server.Proto.Platform;

namespace Whey.Server.Grpc;

[Authorize]
public class PackageRetrieverServiceImpl : PackageRetrieverService.PackageRetrieverServiceBase
{
	private readonly WheyContext _db;
	private readonly IBinStorageService _binStorageService;
	private readonly BinStorageServiceOptions _storageOpts;

	public PackageRetrieverServiceImpl(
			WheyContext db,
			IBinStorageService binStorageService,
			IOptions<BinStorageServiceOptions> storageOptions)
	{
		_db = db;
		_binStorageService = binStorageService;
		_storageOpts = storageOptions.Value;
	}
	// return dl link for package
	// client downloads over http cuz i am NOT streaming the bytes to client
	// TODO: instead of making azure blob storage container public, provide a SAS token valid for ~15 minutes
	// there is nothing sensitive in blob storage, so it honestly doesn't matter much (but is good practice)
	public override async Task<GetPackageResponse> GetPackage(GetPackageRequest request, ServerCallContext context)
	{
		if (!Enum.TryParse<ProtoPlatform>(request.Platform, ignoreCase: true, out var platform))
		{
			var status = new Status(StatusCode.InvalidArgument, "Invalid platform.");
			throw new RpcException(status);
		}

		var pkg = await _db.Packages
			.AsNoTracking()
			.FirstOrDefaultAsync(p => p.Owner == request.Owner && p.Repo == request.Repo);

		if (pkg is null)
		{
			// TODO: instead of doing this, maybe retrieve this from GH and upload to blob storage instead?
			var status = new Status(StatusCode.NotFound, "Package not found.");
			throw new RpcException(status);
		}

		if (!pkg.ReleaseAssets.TryGetValue(PlatformConverter.ConvertProtoToCore(platform), out var assets) || assets.Length == 0)
		{
			var status = new Status(StatusCode.NotFound, "No asset found for the requested platform.");
			throw new RpcException(status);
		}

		string blobPath = $"{pkg.Owner}/{pkg.Repo}/{assets[0]}";

		var sasUri = _binStorageService.GenerateBlobSasUri(
				_storageOpts.ContainerName,
				blobPath,
				TimeSpan.FromMinutes(5)); // WARNING: hardcoded for now, remove magic number later.

		return new GetPackageResponse
		{
			DlLink = sasUri.ToString()
		};
	}
}
