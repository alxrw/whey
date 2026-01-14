using AwesomeAssertions;
using Whey.Infra.Extensions;

namespace Whey.Tests.Unit;

public class DateTimeOffsetExtensionsTests
{
	[Fact]
	public void AddJitter_ResultIsWithinBounds()
	{
		var baseTime = DateTimeOffset.UtcNow;
		var jitterSeconds = 60;
		var minExpected = baseTime.AddSeconds(-jitterSeconds);
		var maxExpected = baseTime.AddSeconds(jitterSeconds);

		// Run multiple iterations to verify statistical correctness
		for (int i = 0; i < 100; i++)
		{
			var result = baseTime.AddJitter(jitterSeconds);

			result.Should().BeOnOrAfter(minExpected);
			result.Should().BeOnOrBefore(maxExpected);
		}
	}

	[Fact]
	public void AddJitter_NegativeDelta_TakesAbsoluteValue()
	{
		var baseTime = DateTimeOffset.UtcNow;
		var jitterSeconds = -60; // Negative
		var minExpected = baseTime.AddSeconds(-60);
		var maxExpected = baseTime.AddSeconds(60);

		for (int i = 0; i < 100; i++)
		{
			var result = baseTime.AddJitter(jitterSeconds);

			result.Should().BeOnOrAfter(minExpected);
			result.Should().BeOnOrBefore(maxExpected);
		}
	}

	[Fact]
	public void AddJitter_ZeroDelta_ReturnsSameTime()
	{
		var baseTime = DateTimeOffset.UtcNow;

		var result = baseTime.AddJitter(0);

		result.Should().Be(baseTime);
	}

	[Fact]
	public void AddJitter_ProducesVariation()
	{
		var baseTime = DateTimeOffset.UtcNow;
		var jitterSeconds = 3600; // 1 hour
		var results = new HashSet<long>();

		// Generate many samples
		for (int i = 0; i < 100; i++)
		{
			var result = baseTime.AddJitter(jitterSeconds);
			results.Add(result.Ticks);
		}

		// Should have variation (not all the same value)
		// With 100 iterations and 7200 possible seconds, we expect many unique values
		results.Count.Should().BeGreaterThan(1);
	}

	[Fact]
	public void AddJitter_SmallDelta_StillWorks()
	{
		var baseTime = DateTimeOffset.UtcNow;
		var jitterSeconds = 1;

		var result = baseTime.AddJitter(jitterSeconds);

		var diff = Math.Abs((result - baseTime).TotalSeconds);
		diff.Should().BeLessThanOrEqualTo(1);
	}
}
