using System.Reflection;
using Octokit;
using Whey.Core.Models;

namespace Whey.Infra.Services;

public interface IAssetMappingService
{
	public IEnumerable<ReleaseAsset> SelectReleaseAsset(
			IEnumerable<ReleaseAsset> assets,
			Platform os,
			ProcessorArchitecture arch,
			bool strictMatchesOnly);
}

public class AssetMappingService : IAssetMappingService
{
	// TODO: create method that uses SelectReleaseAsset in order to map all release assets to their respective platforms
	// and store the results in Package.ReleaseAssets

	// INFO: Method ported from parm's selectReleaseAsset function in internal/core/installer/release.go
	public IEnumerable<ReleaseAsset> SelectReleaseAsset(
			IEnumerable<ReleaseAsset> assets,
			Platform os,
			ProcessorArchitecture arch,
			bool strictMatchesOnly = false)
	{
		var oses = new Dictionary<Platform, string[]>{
			{ Platform.Windows, ["windows", "win64", "win32", "win"] },
			{ Platform.Darwin, ["macos", "darwin", "mac", "osx"] },
			{ Platform.Linux, ["linux"] },
		};
		var arches = new Dictionary<ProcessorArchitecture, string[]>
		{
			{ ProcessorArchitecture.Amd64, ["amd64", "x86_64", "x64", "64bit", "64-bit"] },
			{ ProcessorArchitecture.X86, ["386", "x86", "i386", "32bit", "32-bit"] },
			// WARNING: C# does not differentiate between ARM64 and ARM (unlike Go)
			// this means that they will be grouped together, and arm64 will be treated the same as arm32 or other arm archs.
			{ ProcessorArchitecture.Arm, ["arm64", "aarch64", "armv7", "armv6", "armhf", "armv7l"] },
		};

		var extPref = new string[] { ".tar.gz", ".tgz", ".tar.xz", ".zip", ".bin", ".AppImage" };
		if (os == Platform.Windows)
		{
			extPref = [".zip", ".exe", ".msi", ".bin"];
		}

		var scoreMods = new Dictionary<string, int>{
			{"musl", -1},
		};
		if (os == Platform.Windows)
		{
			scoreMods = [];
		}
		var scoredMatches = new List<(ReleaseAsset Asset, int Score)>();
		const int osMatch = 11;
		const int archMatch = 7;
		const int prefMatch = 3;
		const int minThresh = osMatch + archMatch;

		foreach (var asset in assets)
		{
			// NOTE: score set to 1 initially to combat a musl score mod
			// ex. if minThresh is met, meaning that the asset met osMatch (11) + archMatch(7) minimum score (18)
			// then if the asset uses musl, it would still be 18 (i.e. 1 + 11 + 7 - 1)
			// then if asset doesn't use musl, then the score would be 19 (i.e. 1 + 11 + 7)
			// there should be a better way to handle this score debuff (not scabale) but this should suffice for now.
			var score = 1;
			var name = asset.Name;

			if (oses.TryGetValue(os, out var osTokens) && ContainsAny(name, osTokens))
			{
				score += osMatch;
			}

			if (arches.TryGetValue(arch, out var archTokens) && ContainsAny(name, archTokens))
			{
				score += archMatch;
			}

			for (var i = 0; i < extPref.Length; i++)
			{
				var ext = extPref[i];
				var mult = (double)prefMatch * (extPref.Length - i);
				var multRounded = (int)Math.Round(mult);
				if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
				{
					score += multRounded;
				}
			}

			foreach (var mod in scoreMods)
			{
				if (name.Contains(mod.Key, StringComparison.OrdinalIgnoreCase))
				{
					score += mod.Value;
				}
			}

			if ((strictMatchesOnly && score >= minThresh) || !strictMatchesOnly)
			{
				scoredMatches.Add((asset, score));
			}
		}

		var sortedMatches = scoredMatches.OrderByDescending(m => m.Score).ToList();

		if (sortedMatches.Count == 0)
		{
			return [];
		}

		var maxScore = sortedMatches[0].Score;
		return sortedMatches
			.TakeWhile(m => m.Score == maxScore)
			.Select(m => m.Asset)
			.ToArray();
	}

	private static bool ContainsAny(string src, string[] tokens)
	{
		return tokens.Any(token => src.Contains(token, StringComparison.OrdinalIgnoreCase));
	}
}
