using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SpireMeter.Core;

/// <summary>Where finished-combat snapshots of a run are kept so a reloaded run can pick up where it left off.</summary>
public interface IRunStore
{
	/// <param name="resumed">The game loaded the run from a save, so a near match on its start time is the same run.</param>
	RunData? Load(string runId, bool resumed = false);

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

	/// <summary>
	/// How far apart two start times of one run can be. Each player's game stamps a new run with its own clock,
	/// but a reloaded co-op run carries the host's stamp, so a joining player's id shifts by the clock difference.
	/// Only used for resumed runs: a fresh run on the same seed minutes later is a different run.
	/// </summary>
	public const long StartTimeSlack = 600;

	public RunData? Load(string runId, bool resumed = false)
	{
		try
		{
			return Read(PathFor(runId), runId) ?? (resumed ? LoadNear(runId) : null);
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

	private static RunData? Read(string path, string? runId)
	{
		if (!File.Exists(path))
		{
			return null;
		}
		RunData? data = JsonSerializer.Deserialize<RunData>(File.ReadAllText(path), JsonOptions);
		return (data != null && data.Format == 1 && (runId == null || data.RunId == runId)) ? data : null;
	}

	/// <summary>The saved run on the same seed whose start time is closest to this one, within <see cref="StartTimeSlack"/> seconds.</summary>
	private RunData? LoadNear(string runId)
	{
		if (!TrySplit(runId, out string seed, out long start) || !Directory.Exists(directory))
		{
			return null;
		}
		RunData? best = null;
		long bestGap = StartTimeSlack;
		foreach (FileInfo file in new DirectoryInfo(directory).GetFiles("*.json"))
		{
			RunData? data;
			try
			{
				data = Read(file.FullName, null);
			}
			catch (Exception)
			{
				continue;
			}
			if (data != null && TrySplit(data.RunId, out string otherSeed, out long otherStart) && otherSeed == seed && Math.Abs(otherStart - start) <= bestGap)
			{
				best = data;
				bestGap = Math.Abs(otherStart - start);
			}
		}
		return best;
	}

	private static bool TrySplit(string runId, out string seed, out long start)
	{
		int at = runId.LastIndexOf('@');
		seed = at > 0 ? runId.Substring(0, at) : "";
		start = 0;
		return at > 0 && long.TryParse(runId.Substring(at + 1), out start);
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
