using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;

namespace SpireMeter.Game;

/// <summary>
/// Mirrors the models the game pushes onto its choice contexts while a relic, power, potion or orb reacts to
/// something. Hooks like block gain get no context, so this is how the meter learns which relic gave the block.
/// It is a best guess shared by all contexts, which is fine because the game runs one action at a time.
/// </summary>
public static class SourceTracker
{
	private const int MaxDepth = 64;

	private static readonly List<AbstractModel> Active = new List<AbstractModel>();

	public static AbstractModel? Current => (Active.Count == 0) ? null : Active[Active.Count - 1];

	public static void Pushed(AbstractModel model)
	{
		if (Active.Count >= MaxDepth)
		{
			Active.RemoveAt(0);
		}
		Active.Add(model);
	}

	public static void Popped(AbstractModel model)
	{
		int index = Active.LastIndexOf(model);
		if (index >= 0)
		{
			Active.RemoveAt(index);
		}
	}

	public static void Clear()
	{
		Active.Clear();
	}
}
