namespace Whey.Core.Models;

public static class Platform
{
	public const int UNSPECIFIED = 0;
	public const int LINUX = 1 << 0;
	public const int WINDOWS = 1 << 1;
	public const int DARWIN = 1 << 2;
}
