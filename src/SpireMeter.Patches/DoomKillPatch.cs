using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;
using SpireMeter.Core;
using SpireMeter.Game;

namespace SpireMeter.Patches;

/// <summary>
/// Doom never goes through damage: <c>DoomPower.DoomKill</c> kills every doomed creature at once. Reading their HP
/// before the kill tells how much Doom took.
/// </summary>
[HarmonyPatch(typeof(DoomPower), "DoomKill")]
public static class DoomKillPatch
{
	private static bool Prepare()
	{
		return CaptureGuard.TargetExists(typeof(DoomPower), "DoomKill");
	}

	private static void Prefix(IReadOnlyList<Creature> creatures)
	{
		CaptureGuard.Run(delegate
		{
			Capture(creatures);
		});
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Capture(IReadOnlyList<Creature> creatures)
	{
		foreach (Creature creature in creatures)
		{
			MeterEvent? meterEvent = GameEventMapper.DoomKilled(creature);
			if (meterEvent != null)
			{
				MeterRecorder.Record(meterEvent);
			}
		}
	}
}
