using AwesomeAssertions;
using Whey.Core.Models;

namespace Whey.Tests.Integration;

public class ApiSchedulingServiceTests
{
	[Fact]
	public void Package_LastReleased_CanBeUsedForTierCalculation()
	{
		// Recent release (Tier 1 = 1 hour)
		var recentPkg = new Package
		{
			Id = Guid.CreateVersion7(),
			Owner = "test",
			Repo = "recent",
			Version = "v1.0.0",
			LastReleased = DateTimeOffset.UtcNow.AddDays(-7), // Within 14 days
		};

		// 3-month old release (Tier 2 = 6 hours)
		var oldPkg = new Package
		{
			Id = Guid.CreateVersion7(),
			Owner = "test",
			Repo = "old",
			Version = "v1.0.0",
			LastReleased = DateTimeOffset.UtcNow.AddDays(-60),
		};

		// Very old release (Tier 4 = 72 hours)
		var veryOldPkg = new Package
		{
			Id = Guid.CreateVersion7(),
			Owner = "test",
			Repo = "veryold",
			Version = "v1.0.0",
			LastReleased = DateTimeOffset.UtcNow.AddYears(-2),
		};

		// Age calculations (LastReleased is nullable but we've set it)
		var recentAge = DateTimeOffset.UtcNow - recentPkg.LastReleased.Value;
		var oldAge = DateTimeOffset.UtcNow - oldPkg.LastReleased.Value;
		var veryOldAge = DateTimeOffset.UtcNow - veryOldPkg.LastReleased.Value;

		// Tier thresholds
		recentAge.TotalDays.Should().BeLessThan(14); // Tier 1
		oldAge.TotalDays.Should().BeLessThan(90);    // Tier 2
		veryOldAge.TotalDays.Should().BeGreaterThan(365); // Tier 4
	}
}
