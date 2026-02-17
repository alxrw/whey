namespace Whey.Server.Converters;

using CorePlatform = Whey.Core.Models.Platform;
using ProtoPlatform = Whey.Server.Proto.Platform;

// convert Whey.Core.Models.Platform to a Whey.Server.Platform
// also convert to a string
public static class PlatformConverter
{
	private const string STRING_Linux = "linux";
	private const string STRING_Windows = "windows";
	private const string STRING_Darwin = "darwin";

	public static ProtoPlatform ConvertCoreToProto(CorePlatform p)
	{
		if (p == CorePlatform.Linux)
		{
			return ProtoPlatform.Linux;
		}
		else if (p == CorePlatform.Windows)
		{
			return ProtoPlatform.Windows;
		}
		else if (p == CorePlatform.Darwin)
		{
			return ProtoPlatform.Darwin;
		}
		return ProtoPlatform.Unspecified;
	}

	public static CorePlatform ConvertProtoToCore(ProtoPlatform p)
	{
		if (p == ProtoPlatform.Linux)
		{
			return CorePlatform.Linux;
		}
		else if (p == ProtoPlatform.Windows)
		{
			return CorePlatform.Windows;
		}
		else if (p == ProtoPlatform.Darwin)
		{
			return CorePlatform.Darwin;
		}
		return CorePlatform.Unspecified;
	}

	public static string ConvertProtoToString(ProtoPlatform p)
	{
		if (p == ProtoPlatform.Linux)
		{
			return STRING_Linux;
		}
		if (p == ProtoPlatform.Windows)
		{
			return STRING_Windows;
		}
		if (p == ProtoPlatform.Darwin)
		{
			return STRING_Darwin;
		}
		return String.Empty;
	}

	public static CorePlatform ConvertStringToCore(string platform)
	{
		string p = platform.ToLower();
		if (p == STRING_Linux)
		{
			return CorePlatform.Linux;
		}
		if (p == STRING_Windows)
		{
			return CorePlatform.Windows;
		}
		if (p == STRING_Darwin)
		{
			return CorePlatform.Darwin;
		}
		return CorePlatform.Unspecified;
	}
}
