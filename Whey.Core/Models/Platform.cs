namespace Whey.Core.Models;

[Flags]
public enum Platform : uint
{
	Unspecified = 0,
	Linux = 1 << 0,
	Windows = 1 << 1,
	Darwin = 1 << 2,
}
