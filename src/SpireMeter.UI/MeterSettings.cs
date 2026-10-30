using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace SpireMeter.UI;

/// <summary>What the player arranged on the meter: where the window sits and which sections are switched on.</summary>
public sealed class MeterSettings
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	/// <summary>Top-left corner in screen pixels. Null until the window has been dragged.</summary>
	public float? X { get; set; }

	public float? Y { get; set; }

	public bool Collapsed { get; set; }

	public float Width { get; set; } = 360f;

	/// <summary>Height of the scrolling area when the player resized it. Null means "fit the content".</summary>
	public float? ContentHeight { get; set; }

	/// <summary>The tab that was open: Run, Fight, Details or Turns.</summary>
	public string View { get; set; } = "Run";

	/// <summary>Look up the latest release on GitHub at launch and offer it when newer. Set false to never ask.</summary>
	public bool CheckForUpdates { get; set; } = true;

	/// <summary>"Tabs" shows one section at a time; "Stacked" shows the chosen sections one below the other.</summary>
	public string Layout { get; set; } = "Tabs";

	/// <summary>Stacked layout: sections top to bottom. Null means the tab order.</summary>
	public List<string>? StackOrder { get; set; }

	/// <summary>Stacked layout: sections switched off.</summary>
	public List<string> StackHidden { get; set; } = new List<string>();

	private static string FilePath => ProjectSettings.GlobalizePath("user://SpireMeter/settings.json");

	public static MeterSettings Load()
	{
		try
		{
			if (File.Exists(FilePath))
			{
				return JsonSerializer.Deserialize<MeterSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new MeterSettings();
			}
		}
		catch (Exception ex)
		{
			Log.Warn("[SpireMeter] could not read settings, using defaults: " + ex.Message);
		}
		return new MeterSettings();
	}

	public void Save()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
			File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
		}
		catch (Exception ex)
		{
			Log.Warn("[SpireMeter] could not save settings: " + ex.Message);
		}
	}
}
