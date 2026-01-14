using System.Reflection;
using AwesomeAssertions;
using Octokit;
using Whey.Core.Models;
using Whey.Infra.Services;
using Whey.Tests.Fakes;

namespace Whey.Tests.Unit;

public class AssetMappingServiceTests
{
	private readonly AssetMappingService _service = new();

	[Fact]
	public void SelectReleaseAsset_WindowsAmd64_ReturnsZipAsset()
	{
		var assets = new[]
		{
			ReleaseAssetFactory.Create("app-linux-amd64.tar.gz"),
			ReleaseAssetFactory.Create("app-windows-amd64.zip"),
			ReleaseAssetFactory.Create("app-darwin-arm64.tar.gz"),
		};

		var result = _service.SelectReleaseAsset(assets, Platform.Windows, ProcessorArchitecture.Amd64, strictMatchesOnly: true);

		result.Should().ContainSingle();
		result.First().Name.Should().Be("app-windows-amd64.zip");
	}

	[Fact]
	public void SelectReleaseAsset_LinuxAmd64_ReturnsTarGzAsset()
	{
		var assets = new[]
		{
			ReleaseAssetFactory.Create("app-linux-amd64.tar.gz"),
			ReleaseAssetFactory.Create("app-windows-amd64.zip"),
			ReleaseAssetFactory.Create("app-darwin-arm64.tar.gz"),
		};

		var result = _service.SelectReleaseAsset(assets, Platform.Linux, ProcessorArchitecture.Amd64, strictMatchesOnly: true);

		result.Should().ContainSingle();
		result.First().Name.Should().Be("app-linux-amd64.tar.gz");
	}

	[Fact]
	public void SelectReleaseAsset_DarwinArm64_ReturnsCorrectAsset()
	{
		var assets = new[]
		{
			ReleaseAssetFactory.Create("app-linux-amd64.tar.gz"),
			ReleaseAssetFactory.Create("app-windows-amd64.zip"),
			ReleaseAssetFactory.Create("app-darwin-arm64.tar.gz"),
			ReleaseAssetFactory.Create("app-macos-aarch64.tar.gz"),
		};

		var result = _service.SelectReleaseAsset(assets, Platform.Darwin, ProcessorArchitecture.Arm, strictMatchesOnly: true);

		result.Should().HaveCountGreaterThanOrEqualTo(1);
		result.Should().Contain(a => a.Name.Contains("darwin") || a.Name.Contains("macos"));
	}

	[Fact]
	public void SelectReleaseAsset_MuslBuild_IsPenalizedOnLinux()
	{
		var assets = new[]
		{
			ReleaseAssetFactory.Create("app-linux-amd64-musl.tar.gz"),
			ReleaseAssetFactory.Create("app-linux-amd64.tar.gz"),
		};

		var result = _service.SelectReleaseAsset(assets, Platform.Linux, ProcessorArchitecture.Amd64, strictMatchesOnly: true);

		result.Should().ContainSingle();
		result.First().Name.Should().Be("app-linux-amd64.tar.gz");
	}

	[Fact]
	public void SelectReleaseAsset_MuslBuild_NotPenalizedOnWindows()
	{
		// musl penalty doesn't apply on Windows
		var assets = new[]
		{
			ReleaseAssetFactory.Create("app-windows-amd64-musl.zip"),
			ReleaseAssetFactory.Create("app-windows-amd64.zip"),
		};

		var result = _service.SelectReleaseAsset(assets, Platform.Windows, ProcessorArchitecture.Amd64, strictMatchesOnly: true);

		// Both should have the same score on Windows (no musl penalty)
		result.Should().HaveCount(2);
	}

	[Fact]
	public void SelectReleaseAsset_StrictMatchesOnly_ReturnsEmptyWhenNoMatch()
	{
		var assets = new[]
		{
			ReleaseAssetFactory.Create("app-linux-amd64.tar.gz"),
		};

		var result = _service.SelectReleaseAsset(assets, Platform.Windows, ProcessorArchitecture.Amd64, strictMatchesOnly: true);

		result.Should().BeEmpty();
	}

	[Fact]
	public void SelectReleaseAsset_NonStrictMode_ReturnsAllSorted()
	{
		var assets = new[]
		{
			ReleaseAssetFactory.Create("app-linux-amd64.tar.gz"),
			ReleaseAssetFactory.Create("random-file.txt"),
		};

		var result = _service.SelectReleaseAsset(assets, Platform.Windows, ProcessorArchitecture.Amd64, strictMatchesOnly: false);

		// Non-strict returns assets even without OS/arch match
		result.Should().NotBeEmpty();
	}

	[Fact]
	public void SelectReleaseAsset_TiedScores_ReturnsMultipleAssets()
	{
		var assets = new[]
		{
			ReleaseAssetFactory.Create("app-linux-x86_64.tar.gz"),
			ReleaseAssetFactory.Create("app-linux-amd64.tar.gz"),
		};

		var result = _service.SelectReleaseAsset(assets, Platform.Linux, ProcessorArchitecture.Amd64, strictMatchesOnly: true);

		// Both match linux and amd64/x86_64, should have same score
		result.Should().HaveCount(2);
	}

	[Fact]
	public void SelectReleaseAsset_EmptyAssetList_ReturnsEmpty()
	{
		var assets = Array.Empty<ReleaseAsset>();

		var result = _service.SelectReleaseAsset(assets, Platform.Linux, ProcessorArchitecture.Amd64);

		result.Should().BeEmpty();
	}

	[Fact]
	public void SelectReleaseAsset_PrefersHigherRankedExtensions()
	{
		// .tar.gz should score higher than .zip for Linux
		var assets = new[]
		{
			ReleaseAssetFactory.Create("app-linux-amd64.zip"),
			ReleaseAssetFactory.Create("app-linux-amd64.tar.gz"),
		};

		var result = _service.SelectReleaseAsset(assets, Platform.Linux, ProcessorArchitecture.Amd64, strictMatchesOnly: true);

		result.Should().ContainSingle();
		result.First().Name.Should().Be("app-linux-amd64.tar.gz");
	}

	[Fact]
	public void SelectReleaseAsset_WindowsPrefersZipOverTarGz()
	{
		var assets = new[]
		{
			ReleaseAssetFactory.Create("app-windows-amd64.tar.gz"),
			ReleaseAssetFactory.Create("app-windows-amd64.zip"),
		};

		var result = _service.SelectReleaseAsset(assets, Platform.Windows, ProcessorArchitecture.Amd64, strictMatchesOnly: true);

		result.Should().ContainSingle();
		result.First().Name.Should().Be("app-windows-amd64.zip");
	}

	[Fact]
	public void SelectReleaseAsset_RecognizesWin64Token()
	{
		var assets = new[]
		{
			ReleaseAssetFactory.Create("app-win64.zip"),
			ReleaseAssetFactory.Create("app-linux-amd64.tar.gz"),
		};

		var result = _service.SelectReleaseAsset(assets, Platform.Windows, ProcessorArchitecture.Amd64, strictMatchesOnly: false);

		result.First().Name.Should().Be("app-win64.zip");
	}

	[Fact]
	public void SelectReleaseAsset_RecognizesMacosToken()
	{
		var assets = new[]
		{
			ReleaseAssetFactory.Create("app-macos-arm64.tar.gz"),
			ReleaseAssetFactory.Create("app-linux-amd64.tar.gz"),
		};

		var result = _service.SelectReleaseAsset(assets, Platform.Darwin, ProcessorArchitecture.Arm, strictMatchesOnly: true);

		result.Should().ContainSingle();
		result.First().Name.Should().Be("app-macos-arm64.tar.gz");
	}
}
