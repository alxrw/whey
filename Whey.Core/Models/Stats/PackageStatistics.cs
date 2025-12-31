using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Whey.Core.Models.Stats;

public class PackageStatistics
{
	[Key]
	public Guid Id { get; set; }

	[ForeignKey("Package")]
	public Guid PackageId { get; set; }

	public InstallStat Installs { get; set; } = new();
	public UpdateStat Updates { get; set; } = new();

	[DatabaseGenerated(DatabaseGeneratedOption.Computed)]
	public long TotalInteractions { get; private set; }
}

public class InstallStat : TrackedStat { }
public class UpdateStat : TrackedStat { }
