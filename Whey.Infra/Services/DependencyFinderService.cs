using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Whey.Infra.Services;

public static partial class DependencyFinderService
{
	[GeneratedRegex(@"^\s*NEEDED\s+(.+)$", RegexOptions.Multiline | RegexOptions.Compiled)]
	internal static partial Regex ObjdumpParsePattern();

	// INFO: This method is a port of Parm's getMissingLibsLinux() function in pkg/deps/deps.go
	public static string[] GetLibsLinux(string binPath)
	{
		var processInfo = new ProcessStartInfo
		{
			FileName = "objdump",
			Arguments = $"-p -- \"{binPath}\"",
			RedirectStandardOutput = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};
		using var process = Process.Start(processInfo);
		if (process is null)
		{
			return [];
		}

		var output = process.StandardOutput.ReadToEnd();
		process.WaitForExit();

		if (process.ExitCode != 0)
		{
			return [];
		}

		return ParseObjdumpOutput(output);
	}

	internal static string[] ParseObjdumpOutput(string output)
	{
		var deps = new List<string>();
		var matches = ObjdumpParsePattern().Matches(output);

		foreach (Match match in matches)
		{
			if (match.Groups.Count == 2)
			{
				var trimmed = match.Groups[1].Value.Trim();
				deps.Add(trimmed);
			}
		}

		return [.. deps];
	}
}
