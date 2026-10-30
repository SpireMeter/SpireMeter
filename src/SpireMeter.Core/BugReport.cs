using System;
using System.IO;
using System.Linq;
using System.Text;

namespace SpireMeter.Core;

/// <summary>The text of bug-report.txt: version, window settings and the end of the newest event log.</summary>
public static class BugReport
{
	/// <summary>Event log lines put in a report: the end of the session is where the bug usually is.</summary>
	public const int MaxLines = 4000;

	public static string Build(string version, string system, string dataDir, DateTime now, int maxLines = MaxLines)
	{
		StringBuilder text = new StringBuilder();
		text.AppendLine($"SpireMeter {version} bug report, {now:yyyy-MM-dd HH:mm}");
		text.AppendLine("System: " + system);
		string settings = Path.Combine(dataDir, "settings.json");
		if (File.Exists(settings))
		{
			text.AppendLine();
			text.AppendLine("settings.json:");
			text.AppendLine(File.ReadAllText(settings).TrimEnd());
		}
		text.AppendLine();
		// Names hold the session's start time, so the last one by name is the current session.
		string? log = Directory.Exists(dataDir) ? Directory.GetFiles(dataDir, "events-*.jsonl").OrderBy((string f) => f).LastOrDefault() : null;
		if (log == null)
		{
			text.AppendLine("No event log found.");
			return text.ToString();
		}
		// The log is still open for writing while the game runs, so read it without asking for exclusive access.
		string[] lines;
		using (FileStream stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
		using (StreamReader reader = new StreamReader(stream))
		{
			lines = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
		}
		int shown = Math.Min(lines.Length, maxLines);
		text.AppendLine($"{Path.GetFileName(log)} (last {shown} of {lines.Length} lines):");
		foreach (string line in lines.Skip(lines.Length - shown))
		{
			text.AppendLine(line.TrimEnd('\r'));
		}
		return text.ToString();
	}
}
