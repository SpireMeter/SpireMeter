using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Creatures;
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

	/// <summary>How long a flashed power stays the fallback source; powers flash just before they act.</summary>
	private const long FlashWindowMs = 2000;

	private static PowerModel? _flashed;

	private static long _flashedAt;

	public static void Flashed(PowerModel power)
	{
		_flashed = power;
		_flashedAt = Environment.TickCount64;
	}

	/// <summary>
	/// The power of <paramref name="owner"/> that flashed just now. Turn-start powers like Noxious Fumes act with an
	/// empty context and no card, so the flash they show right before acting is the only trace of them.
	/// </summary>
	public static AbstractModel? FlashedBy(Creature? owner)
	{
		return (owner != null && _flashed != null && _flashed.Owner == owner && Environment.TickCount64 - _flashedAt <= FlashWindowMs) ? _flashed : null;
	}

	public static void Clear()
	{
		Active.Clear();
		_flashed = null;
	}
}
