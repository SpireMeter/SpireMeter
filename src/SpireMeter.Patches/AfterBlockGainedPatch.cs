using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SpireMeter.Core;
using SpireMeter.Game;

namespace SpireMeter.Patches;

/// <summary>Block a player's creature gained, after all modifiers, credited to the card's owner.</summary>
[HarmonyPatch(typeof(Hook), "AfterBlockGained")]
public static class AfterBlockGainedPatch
{
	private static bool Prepare()
	{
		return CaptureGuard.TargetExists("AfterBlockGained");
	}

	private static void Prefix(Creature __1, decimal __2, CardModel? __4)
	{
		CaptureGuard.Run(delegate
		{
			Capture(__1, __2, __4);
		});
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Capture(Creature creature, decimal amount, CardModel? cardSource)
	{
		MeterEvent? meterEvent = GameEventMapper.BlockGained(creature, amount, cardSource);
		if (meterEvent != null)
		{
			MeterRecorder.Record(meterEvent);
		}
	}
}
