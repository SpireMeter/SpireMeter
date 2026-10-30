using System.IO;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using SpireMeter.Core;

namespace SpireMeter.Game;

public static class MeterRecorder
{
	private static EventLog? _log;

	public static MeterState State { get; private set; } = new MeterState();

	public static void Initialize()
	{
		string root = ProjectSettings.GlobalizePath("user://SpireMeter");
		void warn(string message) => Log.Warn("[SpireMeter] " + message);
		MigrateLegacyData(ProjectSettings.GlobalizePath("user://VibeNight"), root, warn);
		_log = new EventLog(root, warn);
		State = new MeterState(new RunStore(Path.Combine(root, "runs"), warn));
	}

	/// <summary>The mod used to be called VibeNight; bring its saved runs and window settings along once.</summary>
	private static void MigrateLegacyData(string legacyRoot, string root, System.Action<string> warn)
	{
		try
		{
			if (Directory.Exists(root) || !Directory.Exists(legacyRoot))
			{
				return;
			}
			Directory.CreateDirectory(Path.Combine(root, "runs"));
			string legacyRuns = Path.Combine(legacyRoot, "runs");
			if (Directory.Exists(legacyRuns))
			{
				foreach (string file in Directory.GetFiles(legacyRuns, "*.json"))
				{
					File.Copy(file, Path.Combine(root, "runs", Path.GetFileName(file)));
				}
			}
			string legacySettings = Path.Combine(legacyRoot, "settings.json");
			if (File.Exists(legacySettings))
			{
				File.Copy(legacySettings, Path.Combine(root, "settings.json"));
			}
		}
		catch (System.Exception ex)
		{
			warn("could not carry over data from the VibeNight folder: " + ex.Message);
		}
	}

	public static void Record(MeterEvent e)
	{
		State.Apply(e);
		_log?.Write(e);
	}
}
