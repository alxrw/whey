using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Whey.Server.Proto;

namespace Whey.Server.Grpc;

[Authorize]
public class PackageRetrieverServiceImpl : PackageRetrieverService.PackageRetrieverServiceBase
{
	public PackageRetrieverServiceImpl()
	{
	}

	// return dl link for package
	// client downloads over http cuz i am NOT streaming the bytes to client
	// TODO: instead of making azure blob storage container public, provide a SAS token valid for ~15 minutes
	// there is nothing sensitive in blob storage, so it honestly doesn't matter much (but is good practice)
	public override async Task<GetPackageResponse> GetPackage(GetPackageRequest request, ServerCallContext context)
	{
		return null;
	}
}
