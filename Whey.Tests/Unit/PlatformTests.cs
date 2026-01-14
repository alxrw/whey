using AwesomeAssertions;
using Whey.Core.Models;

namespace Whey.Tests.Unit;

public class PlatformTests
{
	[Fact]
	public void BitwiseOr_CombinesFlags()
	{
		var combined = Platform.Linux | Platform.Windows;

		combined.HasFlags(Platform.Linux).Should().BeTrue();
		combined.HasFlags(Platform.Windows).Should().BeTrue();
		combined.HasFlags(Platform.Darwin).Should().BeFalse();
	}

	[Fact]
	public void BitwiseAnd_ExtractsFlags()
	{
		var combined = Platform.Linux | Platform.Windows | Platform.Darwin;

		var extracted = combined & Platform.Linux;

		extracted.Should().Be(Platform.Linux);
	}

	[Fact]
	public void BitwiseXor_TogglesFlags()
	{
		var combined = Platform.Linux | Platform.Windows;

		var toggled = combined ^ Platform.Linux;

		toggled.HasFlags(Platform.Linux).Should().BeFalse();
		toggled.HasFlags(Platform.Windows).Should().BeTrue();
	}

	[Fact]
	public void HasFlags_SingleFlag_ReturnsCorrectResult()
	{
		var platform = Platform.Linux;

		platform.HasFlags(Platform.Linux).Should().BeTrue();
		platform.HasFlags(Platform.Windows).Should().BeFalse();
		platform.HasFlags(Platform.Darwin).Should().BeFalse();
	}

	[Fact]
	public void HasFlags_MultipleFlags_ChecksAllRequired()
	{
		var combined = Platform.Linux | Platform.Windows;
		var required = Platform.Linux | Platform.Windows;

		combined.HasFlags(required).Should().BeTrue();
	}

	[Fact]
	public void HasFlags_PartialMatch_ReturnsFalse()
	{
		var combined = Platform.Linux;
		var required = Platform.Linux | Platform.Windows;

		combined.HasFlags(required).Should().BeFalse();
	}

	[Fact]
	public void GetFlags_SingleFlag_ReturnsSingleElement()
	{
		var platform = Platform.Linux;

		var flags = platform.GetFlags();

		flags.Should().ContainSingle();
		flags.Should().Contain(Platform.Linux);
	}

	[Fact]
	public void GetFlags_MultipleFlags_ReturnsAllSetFlags()
	{
		var combined = Platform.Linux | Platform.Windows | Platform.Darwin;

		var flags = combined.GetFlags();

		flags.Should().HaveCount(3);
		flags.Should().Contain(Platform.Linux);
		flags.Should().Contain(Platform.Windows);
		flags.Should().Contain(Platform.Darwin);
	}

	[Fact]
	public void GetFlags_Unspecified_ReturnsUnspecified()
	{
		var platform = Platform.Unspecified;

		var flags = platform.GetFlags();

		flags.Should().ContainSingle();
		flags.Should().Contain(Platform.Unspecified);
	}

	[Fact]
	public void Equality_SamePlatform_ReturnsTrue()
	{
		var p1 = Platform.Linux;
		var p2 = Platform.Linux;

		(p1 == p2).Should().BeTrue();
		p1.Equals(p2).Should().BeTrue();
	}

	[Fact]
	public void Equality_DifferentPlatform_ReturnsFalse()
	{
		var p1 = Platform.Linux;
		var p2 = Platform.Windows;

		(p1 == p2).Should().BeFalse();
		(p1 != p2).Should().BeTrue();
	}

	[Fact]
	public void Equality_CombinedFlags_WorksCorrectly()
	{
		var p1 = Platform.Linux | Platform.Windows;
		var p2 = Platform.Linux | Platform.Windows;
		var p3 = Platform.Linux | Platform.Darwin;

		(p1 == p2).Should().BeTrue();
		(p1 == p3).Should().BeFalse();
	}

	[Fact]
	public void Equality_NullComparison_HandledCorrectly()
	{
		Platform? p1 = Platform.Linux;
		Platform? p2 = null;

		(p1 == p2).Should().BeFalse();
		(p2 == p1).Should().BeFalse();
	}

	[Fact]
	public void Equality_BothNull_ReturnsTrue()
	{
		Platform? p1 = null;
		Platform? p2 = null;

		(p1 == p2).Should().BeTrue();
	}

	[Fact]
	public void GetHashCode_SamePlatform_ReturnsSameHash()
	{
		var p1 = Platform.Linux;
		var p2 = Platform.Linux;

		p1.GetHashCode().Should().Be(p2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_DifferentPlatform_ReturnsDifferentHash()
	{
		var p1 = Platform.Linux;
		var p2 = Platform.Windows;

		p1.GetHashCode().Should().NotBe(p2.GetHashCode());
	}
}
