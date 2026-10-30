using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SpireMeter.Core;

/// <summary>Where finished-combat snapshots of a run are kept so a reloaded run can pick up where it left off.</summary>
public interface IRunStore
{
	RunData? Load(string runId);

	void Save(RunData data);
}

/// <summary>One JSON file per run. Never throws: a broken store must not break the game or the meter.</summary>
public sealed class RunStore(string directory, Action<string> onError) : IRunStore
{
	private const int MaxRuns = 50;

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public RunData? Load(string runId)
	{
		try
		{
			string path = PathFor(runId);
			if (!File.Exists(path))
			{
				return null;
			}
			RunData? data = JsonSerializer.Deserialize<RunData>(File.ReadAllText(path), JsonOptions);
			return (data != null && data.Format == 1 && data.RunId == runId) ? data : null;
		}
		catch (Exception ex)
		{
			onError("could not load saved run " + runId + ": " + ex.Message);
			return null;
		}
	}

	public void Save(RunData data)
	{
		try
		{
			Directory.CreateDirectory(directory);
			string path = PathFor(data.RunId);
			string temp = path + ".tmp";
			File.WriteAllText(temp, JsonSerializer.Serialize(data, JsonOptions));
			File.Move(temp, path, overwrite: true);
			Prune();
		}
		catch (Exception ex)
		{
			onError("could not save run " + data.RunId + ": " + ex.Message);
		}
	}

	private string PathFor(string runId)
	{
		string name = string.Concat(runId.Select((char c) => Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c));
		return Path.Combine(directory, name + ".json");
	}

	private void Prune()
	{
		foreach (FileInfo stale in new DirectoryInfo(directory).GetFiles("*.json").OrderByDescending((FileInfo f) => f.LastWriteTimeUtc).Skip(MaxRuns))
		{
			stale.Delete();
		}
	}
}
