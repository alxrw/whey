using AwesomeAssertions;
using Whey.Server.Converters;
using CorePlatform = Whey.Core.Models.Platform;
using ProtoPlatform = Whey.Server.Proto.Platform;

namespace Whey.Tests.Unit;

public class PlatformConverterTests
{
	[Fact]
	public void ConvertCoreToProto_Linux_ReturnsLinux()
	{
		var result = PlatformConverter.ConvertCoreToProto(CorePlatform.Linux);
		result.Should().Be(ProtoPlatform.Linux);
	}

	[Fact]
	public void ConvertCoreToProto_Windows_ReturnsWindows()
	{
		var result = PlatformConverter.ConvertCoreToProto(CorePlatform.Windows);
		result.Should().Be(ProtoPlatform.Windows);
	}

	[Fact]
	public void ConvertCoreToProto_Darwin_ReturnsDarwin()
	{
		var result = PlatformConverter.ConvertCoreToProto(CorePlatform.Darwin);
		result.Should().Be(ProtoPlatform.Darwin);
	}

	[Fact]
	public void ConvertCoreToProto_Unspecified_ReturnsUnspecified()
	{
		var result = PlatformConverter.ConvertCoreToProto(CorePlatform.Unspecified);
		result.Should().Be(ProtoPlatform.Unspecified);
	}

	[Fact]
	public void ConvertProtoToString_Linux_ReturnsLinuxString()
	{
		var result = PlatformConverter.ConvertProtoToString(ProtoPlatform.Linux);
		result.Should().Be("linux");
	}

	[Fact]
	public void ConvertProtoToString_Windows_ReturnsWindowsString()
	{
		var result = PlatformConverter.ConvertProtoToString(ProtoPlatform.Windows);
		result.Should().Be("windows");
	}

	[Fact]
	public void ConvertProtoToString_Darwin_ReturnsDarwinString()
	{
		var result = PlatformConverter.ConvertProtoToString(ProtoPlatform.Darwin);
		result.Should().Be("darwin");
	}

	[Fact]
	public void ConvertProtoToString_Unspecified_ReturnsEmptyString()
	{
		var result = PlatformConverter.ConvertProtoToString(ProtoPlatform.Unspecified);
		result.Should().BeEmpty();
	}

	[Fact]
	public void ConvertStringToCore_Linux_ReturnsLinux()
	{
		var result = PlatformConverter.ConvertStringToCore("linux");
		result.Should().Be(CorePlatform.Linux);
	}

	[Fact]
	public void ConvertStringToCore_Windows_ReturnsWindows()
	{
		var result = PlatformConverter.ConvertStringToCore("windows");
		result.Should().Be(CorePlatform.Windows);
	}

	[Fact]
	public void ConvertStringToCore_Darwin_ReturnsDarwin()
	{
		var result = PlatformConverter.ConvertStringToCore("darwin");
		result.Should().Be(CorePlatform.Darwin);
	}

	[Fact]
	public void ConvertStringToCore_InvalidString_ReturnsUnspecified()
	{
		var result = PlatformConverter.ConvertStringToCore("invalid");
		result.Should().Be(CorePlatform.Unspecified);
	}

	[Fact]
	public void ConvertStringToCore_EmptyString_ReturnsUnspecified()
	{
		var result = PlatformConverter.ConvertStringToCore("");
		result.Should().Be(CorePlatform.Unspecified);
	}

	[Theory]
	[InlineData("Linux")]
	[InlineData("LINUX")]
	[InlineData("lInUx")]
	public void ConvertStringToCore_CaseMismatch_ReturnsCorrect(string input)
	{
		var result = PlatformConverter.ConvertStringToCore(input);
		result.Should().Be(CorePlatform.Linux);
	}
}
