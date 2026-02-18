using Azure.Storage.Blobs;
using Whey.Infra.Services;

namespace Whey.Tests.Fakes;

public class FakeBinStorageService : IBinStorageService
{
	public List<(string Container, string FileName)> UploadedFiles { get; } = [];

	public BlobServiceClient GetBinStorageServiceClient()
	{
		// Return a fake client - won't be used in tests since UploadBinaryAsync is overridden
		// In real usage this would throw, but our tests won't call the actual Azure methods
		throw new NotImplementedException("FakeBinStorageService does not support GetBinStorageServiceClient");
	}

	public Task UploadBinaryAsync(BlobContainerClient containerClient, string fileName, Stream fileStream)
	{
		UploadedFiles.Add((containerClient.Name, fileName));
		return Task.CompletedTask;
	}

	public Uri GenerateBlobSasUri(string containerName, string blobPath, TimeSpan validFor)
	{
		return new Uri("uri");
	}
}
