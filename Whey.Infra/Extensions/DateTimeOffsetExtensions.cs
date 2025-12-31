using System.Security.Cryptography;

namespace Whey.Infra.Extensions;

public static class DateTimeOffsetExtensions
{
	public static DateTimeOffset AddJitter(this DateTimeOffset time, int jitterDelta)
	{
		int san = Math.Abs(jitterDelta);
		int jitterValue = RandomNumberGenerator.GetInt32(-san, san + 1);

		TimeSpan jitter = TimeSpan.FromSeconds(jitterValue);

		return time.Add(jitter);
	}
}
