namespace Whey.Core.Models.Stats;

public abstract class TrackedStat
{
	public SortedDictionary<DateTimeOffset, uint> History { get; init; } = [];
	public long Count => GetCountWithinRange(History.First().Key, History.Last().Key);

	public long GetCountWithinRange(DateTimeOffset start, DateTimeOffset end)
	{
		return History
			.Where(kvp => kvp.Key >= start && kvp.Key <= end)
			.Sum(kvp => kvp.Value);
	}

	public void Track(uint amt = 1)
	{
		var now = DateTimeOffset.UtcNow;
		var bucket = new DateTimeOffset(new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc));

		if (!History.ContainsKey(bucket))
		{
			History[bucket] = 0;
		}

		History[bucket] += amt;
	}
}
