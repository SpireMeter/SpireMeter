using System;
using System.Text.Json;

namespace SpireMeter.Core;

/// <summary>Reads GitHub's "latest release" answer and decides whether it is newer than the running build.</summary>
public static class ReleaseCheck
{
	/// <summary>The version from a release's tag ("v1.0.1" gives "1.0.1"), or null when the answer has no usable tag.</summary>
	public static string? LatestVersion(string json)
	{
		try
		{
			using JsonDocument doc = JsonDocument.Parse(json);
			if (doc.RootElement.TryGetProperty("tag_name", out JsonElement tag) && tag.ValueKind == JsonValueKind.String)
			{
				string version = (tag.GetString() ?? "").Trim().TrimStart('v', 'V');
				return Version.TryParse(version, out _) ? version : null;
			}
		}
		catch (JsonException)
		{
		}
		return null;
	}

	public static bool IsNewer(string? latest, string current)
	{
		return Version.TryParse(latest, out Version? a) && Version.TryParse(current, out Version? b) && a > b;
	}
}
