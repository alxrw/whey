using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Whey.Infra.Services;

public interface IDependencyFinderService { }

public class DependencyFinderService
{
	public string[] GetLibsLinux(string binPath)
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

		var deps = new List<string>();
		var pattern = new Regex(@"^\s*NEEDED\s+(.+)$", RegexOptions.Multiline);
		var matches = pattern.Matches(output);

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
