namespace Whey.Tests.Fakes;

public class FakeHttpMessageHandler : HttpMessageHandler
{
	private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

	public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
	{
		_handler = handler;
	}

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		return Task.FromResult(_handler(request));
	}
}

public class FakeHttpClientFactory : IHttpClientFactory
{
	private readonly HttpMessageHandler _handler;

	public FakeHttpClientFactory(HttpMessageHandler handler)
	{
		_handler = handler;
	}

	public HttpClient CreateClient(string name)
	{
		return new HttpClient(_handler);
	}
}
