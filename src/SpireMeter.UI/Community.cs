using System;
using System.IO;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using SpireMeter.Core;

namespace SpireMeter.UI;

/// <summary>The links to the project: checking for a newer release, and filing a bug report.</summary>
public static class Community
{
	public const string Repo = "SpireMeter/SpireMeter";

	public const string ReleasesUrl = "https://github.com/" + Repo + "/releases/latest";

	public const string LatestReleaseApi = "https://api.github.com/repos/" + Repo + "/releases/latest";

	public static string BugFormUrl => "https://github.com/" + Repo + "/issues/new?template=bug_report.yml&version=" + Uri.EscapeDataString(ModEntry.Version);

	private static string DataDir => ProjectSettings.GlobalizePath("user://SpireMeter");

	/// <summary>Set once the launch check finds a newer release; the window's timer picks it up on the game's thread.</summary>
	public static volatile string? NewerVersion;

	/// <summary>Asks GitHub once for the latest release, off the game's thread. Offline, rate-limited or anything unexpected: stays quiet.</summary>
	public static void CheckForUpdate()
	{
		Task.Run(async delegate
		{
			try
			{
				using System.Net.Http.HttpClient http = new System.Net.Http.HttpClient
				{
					Timeout = TimeSpan.FromSeconds(10)
				};
				// GitHub's API turns away requests without a User-Agent.
				http.DefaultRequestHeaders.UserAgent.ParseAdd("SpireMeter/" + ModEntry.Version);
				http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
				string? latest = ReleaseCheck.LatestVersion(await http.GetStringAsync(LatestReleaseApi));
				if (ReleaseCheck.IsNewer(latest, ModEntry.Version))
				{
					NewerVersion = latest;
				}
			}
			catch (Exception ex)
			{
				Log.Info("[SpireMeter] update check skipped: " + ex.Message);
			}
		});
	}

	/// <summary>
	/// Writes bug-report.txt, shows it in the file browser to drag into the report, and opens the bug form with
	/// the version filled in.
	/// </summary>
	public static void ReportBug()
	{
		string? file = null;
		try
		{
			Directory.CreateDirectory(DataDir);
			file = Path.Combine(DataDir, "bug-report.txt");
			File.WriteAllText(file, BugReport.Build(ModEntry.Version, OS.GetName() + " " + OS.GetVersion(), DataDir, DateTime.Now));
		}
		catch (Exception ex)
		{
			Log.Warn("[SpireMeter] could not write the bug report file: " + ex.Message);
			file = null;
		}
		if (file != null)
		{
			if (OS.GetName() == "Windows")
			{
				// Only the path may be quoted: Explorer ignores a fully quoted "/select,..." when the path has spaces,
				// which is how Godot's own process call would pass it.
				System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + file + "\"");
			}
			else
			{
				OS.ShellOpen(DataDir);
			}
		}
		OS.ShellOpen(BugFormUrl);
	}
}
