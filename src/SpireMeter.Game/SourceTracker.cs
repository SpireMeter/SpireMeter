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

	/// <param name="Hook">Entered through a hook the game does not push (arrow hooks, cards generated).</param>
	private readonly record struct Entry(AbstractModel Model, bool Hook);

	private static readonly List<Entry> Active = new List<Entry>();

	public static AbstractModel? Current => (Active.Count == 0) ? null : Active[Active.Count - 1].Model;

	/// <summary>
	/// The model reacting in an unpushed hook, when nothing has been pushed on top of it. Its context still names
	/// the card that set it off, so without this Explosive Arrows' damage would go to the card that shot the arrow.
	/// </summary>
	public static AbstractModel? HookCurrent => (Active.Count == 0 || !Active[Active.Count - 1].Hook) ? null : Active[Active.Count - 1].Model;

	public static void Pushed(AbstractModel model)
	{
		Add(new Entry(model, Hook: false));
	}

	public static void Popped(AbstractModel model)
	{
		Remove(new Entry(model, Hook: false));
	}

	public static void HookEntered(AbstractModel model)
	{
		Add(new Entry(model, Hook: true));
	}

	/// <summary>Called when a model's unpushed hook is done, even if it failed.</summary>
	public static void HookFinished(AbstractModel model)
	{
		Remove(new Entry(model, Hook: true));
	}

	private static void Add(Entry entry)
	{
		if (Active.Count >= MaxDepth)
		{
			Active.RemoveAt(0);
		}
		Active.Add(entry);
	}

	private static void Remove(Entry entry)
	{
		int index = Active.LastIndexOf(entry);
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
