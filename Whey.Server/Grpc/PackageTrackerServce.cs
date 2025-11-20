using Grpc.Core;
using Whey.Server.Proto;

namespace Whey.Server.Grpc;

public class PackageTrackerServiceImpl : PackageTrackerService.PackageTrackerServiceBase
{
	public override Task<TrackResponse> Track(TrackRequest request, ServerCallContext context)
	{
		return null!;
	}
}
