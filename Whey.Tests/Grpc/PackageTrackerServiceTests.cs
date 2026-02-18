using AwesomeAssertions;
using CorePlatform = Whey.Core.Models.Platform;

namespace Whey.Tests.Grpc;

public class PackageTrackerServiceTests
{
	// Why are PlatformConverter tests here?
	// TODO: move outta here
	[Fact]
	public void PlatformConverter_ConvertsLinux()
	{
		var result = Server.Converters.PlatformConverter.ConvertStringToCore("linux");
		result.Should().Be(CorePlatform.Linux);
	}

	[Fact]
	public void PlatformConverter_ConvertsWindows()
	{
		var result = Server.Converters.PlatformConverter.ConvertStringToCore("windows");
		result.Should().Be(CorePlatform.Windows);
	}

	[Fact]
	public void PlatformConverter_ConvertsDarwin()
	{
		var result = Server.Converters.PlatformConverter.ConvertStringToCore("darwin");
		result.Should().Be(CorePlatform.Darwin);
	}

	[Fact]
	public void PlatformConverter_ConvertsUnknownToUnspecified()
	{
		var result = Server.Converters.PlatformConverter.ConvertStringToCore("unknown");
		result.Should().Be(CorePlatform.Unspecified);
	}

	[Fact]
	public void PackageStatistics_CanTrackInstalls()
	{
		var stats = new Core.Models.Stats.PackageStatistics
		{
			Id = Guid.CreateVersion7(),
			PackageId = Guid.CreateVersion7(),
			Installs = new Core.Models.Stats.InstallStat(),
		};

		stats.Installs.Track();
		stats.Installs.Track(5);

		// Should have tracked 6 total in the current hour bucket
		var start = DateTimeOffset.UtcNow.AddHours(-1);
		var end = DateTimeOffset.UtcNow.AddHours(1);
		var count = stats.Installs.GetCountWithinRange(start, end);
		count.Should().Be(6);
	}
}
