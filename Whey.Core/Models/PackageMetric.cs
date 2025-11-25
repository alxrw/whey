using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Whey.Core.Models;

public class PackageMetric
{
	[Key]
	public long Id { get; set; } // BigInt (Identity)

	// Foreign Key to Package
	[ForeignKey("Package")]
	public Guid PackageId { get; set; }
	public virtual Package Package { get; set; } = null!;
	public MetricType Type { get; set; }

	// Time Bucket (Truncated to Hour or Day for aggregation)
	public DateTime Timestamp { get; set; }
	public int Count { get; set; }
}

public enum MetricType
{
	Install = 0,
	Update = 1
}
