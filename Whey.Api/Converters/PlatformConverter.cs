namespace Whey.Api.Converters;

using CorePlatform = Whey.Core.Models.Platform;
using ProtoPlatform = Whey.Api.Proto.Platform;

// convert Whey.Core.Models.Platform to a Whey.Api.Platform
// also convert to a string
public static class PlatformConverter
{
	private const string STRING_LINUX = "linux";
	private const string STRING_WINDOWS = "windows";
	private const string STRING_DARWIN = "darwin";

	public static ProtoPlatform ConvertCoreToProto(CorePlatform p)
	{
		if (p == CorePlatform.LINUX)
		{
			return ProtoPlatform.Linux;
		}
		else if (p == CorePlatform.WINDOWS)
		{
			return ProtoPlatform.Windows;
		}
		else if (p == CorePlatform.DARWIN)
		{
			return ProtoPlatform.Darwin;
		}
		return ProtoPlatform.Unspecified;
	}

	public static string ConvertProtoToString(ProtoPlatform p)
	{
		if (p == ProtoPlatform.Linux)
		{
			return STRING_LINUX;
		}
		if (p == ProtoPlatform.Windows)
		{
			return STRING_WINDOWS;
		}
		if (p == ProtoPlatform.Darwin)
		{
			return STRING_DARWIN;
		}
		return String.Empty;
	}
}
