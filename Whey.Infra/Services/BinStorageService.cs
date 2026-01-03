using Azure.Identity;
using Azure.Storage.Blobs;

namespace Whey.Infra.Services;

public interface IBinStorageService
{
	public BlobServiceClient GetBinStorageServiceClient(string acctName);
	public Task UploadBinaryAsync(BlobContainerClient containerClient, string fileName, Stream fileStream);
}

public class BinStorageService : IBinStorageService
{
	public BlobServiceClient GetBinStorageServiceClient(string acctName)
	{
		BlobServiceClient client = new(
			new Uri($"https://{acctName}.blob.core.windows.net"),
			new DefaultAzureCredential());

		return client;
	}

	public async Task UploadBinaryAsync(BlobContainerClient containerClient, string fileName, Stream fileStream)
	{
		BlobClient blobClient = containerClient.GetBlobClient(fileName);
		await blobClient.UploadAsync(fileStream, overwrite: true);
		// up to caller to close the fileStream
	}
}
