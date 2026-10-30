using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SpireMeter.Core;

public sealed class EventLog : IDisposable
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = false
	};

	private readonly string _directory;

	private readonly Func<DateTime> _clock;

	private readonly Action<string> _onError;

	private StreamWriter? _writer;

	private bool _failed;

	public string? FilePath { get; private set; }

	public EventLog(string directory, Action<string> onError, Func<DateTime>? clock = null)
	{
		_directory = directory;
		_onError = onError;
		_clock = clock ?? ((Func<DateTime>)(() => DateTime.Now));
	}

	public void Write(MeterEvent e)
	{
		if (_failed)
		{
			return;
		}
		try
		{
			if (_writer == null)
			{
				_writer = Open();
			}
			JsonObject jsonObject = new JsonObject
			{
				["at"] = _clock().ToString("O"),
				["type"] = e.GetType().Name
			};
			if (JsonSerializer.SerializeToNode(e, e.GetType(), JsonOptions) is JsonObject jsonObject2)
			{
				foreach (KeyValuePair<string, JsonNode> item in jsonObject2)
				{
					item.Deconstruct(out var key, out var value);
					string propertyName = key;
					jsonObject[propertyName] = value?.DeepClone();
				}
			}
			_writer.WriteLine(jsonObject.ToJsonString(JsonOptions));
		}
		catch (Exception ex)
		{
			_failed = true;
			_onError("event log disabled after an error: " + ex.Message);
		}
	}

	public void Dispose()
	{
		_writer?.Dispose();
		_writer = null;
	}

	/// <summary>One log per game session; only the newest few are kept so the folder never grows without end.</summary>
	public const int KeptLogs = 30;

	private void Prune()
	{
		try
		{
			// Names hold the start time, so name order is age order.
			foreach (string old in Directory.GetFiles(_directory, "events-*.jsonl").OrderByDescending((string f) => f).Skip(KeptLogs - 1))
			{
				File.Delete(old);
			}
		}
		catch (Exception ex)
		{
			_onError("could not clear old event logs: " + ex.Message);
		}
	}

	private StreamWriter Open()
	{
		Directory.CreateDirectory(_directory);
		Prune();
		FilePath =Path.Combine(_directory, $"events-{_clock():yyyy-MM-dd_HH-mm-ss}.jsonl");
		return new StreamWriter(new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
		{
			AutoFlush = true
		};
	}
}
