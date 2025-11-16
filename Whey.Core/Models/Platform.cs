namespace Whey.Core.Models;

public class Platform
{
	public static readonly Platform UNSPECIFIED = new(0);
	public static readonly Platform LINUX = new(1 << 0);
	public static readonly Platform WINDOWS = new(1 << 1);
	public static readonly Platform DARWIN = new(1 << 2);

	private readonly uint _value;

	private Platform(uint value)
	{
		_value = value;
	}

	public bool HasFlags(Platform flags) => (_value & flags._value) == flags._value;

	public ICollection<Platform> GetFlags()
	{
		List<Platform> res = new(1 << 2);
		Platform[] flags = [UNSPECIFIED, LINUX, WINDOWS, DARWIN];

		foreach (Platform f in flags)
		{
			if ((this & f) == f && this != UNSPECIFIED)
			{
				res.Add(f);
			}
		}
		if (res.Count == 0)
		{
			return [UNSPECIFIED];
		}

		return res;
	}

	public static Platform operator ^(Platform left, Platform right) => new(left._value ^ right._value);
	public static Platform operator |(Platform left, Platform right) => new(left._value | right._value);
	public static Platform operator &(Platform left, Platform right) => new(left._value & right._value);

	public static bool operator ==(Platform? left, Platform? right)
	{
		if (left is null)
		{
			return right is null;
		}
		if (right is null)
		{
			return false;
		}
		return left._value == right._value;
	}
	public static bool operator !=(Platform? left, Platform? right) => !(left!._value == right!._value);

	public override bool Equals(object? obj) => obj is Platform other && this == other;
	public override int GetHashCode() => _value.GetHashCode();
}
