using AwesomeAssertions;
using Whey.Core.Models.Stats;

namespace Whey.Tests.Unit;

public class TrackedStatTests
{
	// InstallStat is a concrete implementation of TrackedStat
	private InstallStat CreateStat() => new();

	[Fact]
	public void Track_DefaultAmount_IncrementsBy1()
	{
		var stat = CreateStat();

		stat.Track();

		stat.History.Should().HaveCount(1);
		stat.History.Values.First().Should().Be(1);
	}

	[Fact]
	public void Track_SpecifiedAmount_IncrementsCorrectly()
	{
		var stat = CreateStat();

		stat.Track(5);

		stat.History.Values.First().Should().Be(5);
	}

	[Fact]
	public void Track_MultipleCalls_AggregatesInSameBucket()
	{
		var stat = CreateStat();

		stat.Track(1);
		stat.Track(2);
		stat.Track(3);

		// All calls within the same hour should aggregate
		stat.History.Should().HaveCount(1);
		stat.History.Values.First().Should().Be(6);
	}

	[Fact]
	public void Track_CreatesBucketWithTruncatedHour()
	{
		var stat = CreateStat();
		var beforeTrack = DateTimeOffset.UtcNow;

		stat.Track();

		var bucket = stat.History.Keys.First();
		bucket.Minute.Should().Be(0);
		bucket.Second.Should().Be(0);
		bucket.Hour.Should().Be(beforeTrack.Hour);
	}

	[Fact]
	public void GetCountWithinRange_SumsCorrectly()
	{
		var stat = CreateStat();
		var now = DateTimeOffset.UtcNow;
		var hourBucket = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);

		// Manually add history entries for testing
		stat.History[hourBucket] = 10;
		stat.History[hourBucket.AddHours(-1)] = 20;
		stat.History[hourBucket.AddHours(-2)] = 30;

		var total = stat.GetCountWithinRange(hourBucket.AddHours(-2), hourBucket);

		total.Should().Be(60);
	}

	[Fact]
	public void GetCountWithinRange_ExcludesOutOfRangeBuckets()
	{
		var stat = CreateStat();
		var now = DateTimeOffset.UtcNow;
		var hourBucket = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);

		stat.History[hourBucket] = 10;
		stat.History[hourBucket.AddHours(-1)] = 20;
		stat.History[hourBucket.AddHours(-5)] = 100; // Out of range

		var total = stat.GetCountWithinRange(hourBucket.AddHours(-2), hourBucket);

		total.Should().Be(30); // Only includes current hour and hour-1
	}

	[Fact]
	public void GetCountWithinRange_IncludesBoundaries()
	{
		var stat = CreateStat();
		var start = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero);
		var end = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

		stat.History[start] = 10;
		stat.History[start.AddHours(1)] = 20;
		stat.History[end] = 30;

		var total = stat.GetCountWithinRange(start, end);

		total.Should().Be(60);
	}

	[Fact]
	public void Count_ReturnsTotal()
	{
		var stat = CreateStat();
		var now = DateTimeOffset.UtcNow;
		var hourBucket = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);

		stat.History[hourBucket] = 10;
		stat.History[hourBucket.AddHours(-1)] = 20;

		// Count sums from first to last key
		stat.Count.Should().Be(30);
	}

	[Fact]
	public void InstallStat_InheritsBehavior()
	{
		var installStat = new InstallStat();

		installStat.Track(5);
		installStat.Track(3);

		installStat.History.Values.First().Should().Be(8);
	}
}
