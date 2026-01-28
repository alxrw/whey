using System.ComponentModel.DataAnnotations;

namespace Whey.Infra.Configuration;

public class BinStorageServiceOptions
{
	[Required]
	public string AccountName { get; set; } = string.Empty;

	[Required]
	public string ContainerName { get; set; } = string.Empty;
}
