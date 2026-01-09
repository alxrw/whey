using System.Threading.RateLimiting;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Whey.Server.Interceptors;

public class RateLimiterInterceptor : Interceptor
{
	private readonly PartitionedRateLimiter<string> _limiter;

	public RateLimiterInterceptor(PartitionedRateLimiter<string> limiter)
	{
		_limiter = limiter;
	}

	public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
		TRequest request,
		ServerCallContext context,
		UnaryServerMethod<TRequest, TResponse> continuation)
	{
		var user = context.GetHttpContext().User;
		var publicKey = user.FindFirst("PublicKey")?.Value;

		// skip rate limit for public methods/endpoints
		if (string.IsNullOrEmpty(publicKey))
		{
			return await continuation(request, context);
		}

		// INFO: blocking method, waits until a permit can be acquired
		using var lease = await _limiter.AcquireAsync(publicKey, permitCount: 1);
		if (lease.IsAcquired)
		{
			return await continuation(request, context);
		}

		var status = new Status(StatusCode.ResourceExhausted, "Too many requests. Please slow down.");
		throw new RpcException(status);
	}
}
