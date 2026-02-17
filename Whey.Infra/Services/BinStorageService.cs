using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace Whey.Infra.Services;

public interface IBinStorageService
{
	public BlobServiceClient GetBinStorageServiceClient();
	public Task UploadBinaryAsync(BlobContainerClient containerClient, string fileName, Stream fileStream);
	public Uri GenerateBlobSasUri(string containerName, string blobPath, TimeSpan validFor);
}

public class BinStorageService : IBinStorageService
{
	private readonly string _accountName;

	public BinStorageService(string accountName)
	{
		_accountName = accountName;
	}

	public BlobServiceClient GetBinStorageServiceClient()
	{
		BlobServiceClient client = new(
			new Uri($"https://{_accountName}.blob.core.windows.net"),
			new DefaultAzureCredential());

		return client;
	}

	public async Task UploadBinaryAsync(BlobContainerClient containerClient, string fileName, Stream fileStream)
	{
		BlobClient blobClient = containerClient.GetBlobClient(fileName);
		await blobClient.UploadAsync(fileStream, overwrite: true);
		// up to caller to close the fileStream
	}

	public Uri GenerateBlobSasUri(string containerName, string blobPath, TimeSpan validFor)
	{
		var client = GetBinStorageServiceClient();

		var delKey = client.GetUserDelegationKey(
				DateTimeOffset.UtcNow,
				DateTimeOffset.UtcNow.Add(validFor));

		const int skewBuffer = -5;
		var sasBuilder = new BlobSasBuilder
		{
			BlobContainerName = containerName,
			BlobName = blobPath,
			Resource = "b",
			StartsOn = DateTimeOffset.UtcNow.AddMinutes(skewBuffer),
			ExpiresOn = DateTimeOffset.UtcNow.Add(validFor),
			Protocol = SasProtocol.Https,
		};
		sasBuilder.SetPermissions(BlobSasPermissions.Read);

		var blobUriBuilder = new BlobUriBuilder(
				client.GetBlobContainerClient(containerName)
				.GetBlobClient(blobPath).Uri)
		{
			Sas = sasBuilder.ToSasQueryParameters(delKey, _accountName)
		};

		return blobUriBuilder.ToUri();
	}
}
