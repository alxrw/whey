using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Whey.Infra.Services;
using Whey.Tests.Fakes;
using Whey.Tests.Fixtures;

namespace Whey.Tests.Integration;

public class PackageSyncServiceTests
{
	[Fact]
	public void PackageSyncService_CanBeCreated()
	{
		// Verify the service can be instantiated
		var messageHandler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
		var httpClientFactory = new FakeHttpClientFactory(messageHandler);
		var binStorageService = new FakeBinStorageService();
		var assetMappingService = new AssetMappingService();
		var logger = NullLogger<PackageSyncService>.Instance;

		using var db = TestDbContextFactory.Create();
		var service = new PackageSyncService(logger, httpClientFactory, db, binStorageService, assetMappingService);

		service.Should().NotBeNull();
	}

	[Fact]
	public async Task FakeHttpMessageHandler_ReturnsConfiguredResponse()
	{
		var expectedContent = "test response";
		var handler = new FakeHttpMessageHandler(request =>
		{
			var response = new HttpResponseMessage(HttpStatusCode.OK);
			response.Content = new StringContent(expectedContent);
			return response;
		});

		var factory = new FakeHttpClientFactory(handler);
		var client = factory.CreateClient("test");

		var response = await client.GetAsync("http://example.com", TestContext.Current.CancellationToken);
		var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		content.Should().Be(expectedContent);
	}

	[Fact]
	public async Task FakeHttpMessageHandler_CanReturnNotModified()
	{
		var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotModified));

		var factory = new FakeHttpClientFactory(handler);
		var client = factory.CreateClient("test");

		var response = await client.GetAsync("http://example.com", TestContext.Current.CancellationToken);

		response.StatusCode.Should().Be(HttpStatusCode.NotModified);
	}

	[Fact]
	public async Task FakeHttpMessageHandler_CanReturnETag()
	{
		var etag = "\"abc123\"";
		var handler = new FakeHttpMessageHandler(request =>
		{
			var response = new HttpResponseMessage(HttpStatusCode.OK);
			response.Headers.ETag = new EntityTagHeaderValue(etag);
			return response;
		});

		var factory = new FakeHttpClientFactory(handler);
		var client = factory.CreateClient("test");

		var response = await client.GetAsync("http://example.com", TestContext.Current.CancellationToken);

		response.Headers.ETag.Should().NotBeNull();
		response.Headers.ETag!.Tag.Should().Be(etag);
	}

	[Fact]
	public void FakeBinStorageService_TracksUploads()
	{
		var service = new FakeBinStorageService();

		// FakeBinStorageService tracks uploads via UploadedFiles list
		service.UploadedFiles.Should().BeEmpty();

		// The service is primarily used to satisfy DI and track what would be uploaded
		service.Should().NotBeNull();
	}
}
