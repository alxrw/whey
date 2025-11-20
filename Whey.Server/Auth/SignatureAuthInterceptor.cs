using Grpc.Core.Interceptors;
using Whey.Infra.Data;

namespace Whey.Server.Auth;

public class SignatureAuthInterceptor : Interceptor
{
	private readonly WheyContext _context;

	public SignatureAuthInterceptor(WheyContext context)
	{
		_context = context;
	}
}
