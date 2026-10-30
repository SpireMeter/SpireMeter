using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SpireMeter.Core;
using SpireMeter.Game;

namespace SpireMeter.Patches;

/// <summary>
/// Cards like Expose remove enemy block directly instead of attacking. The block is gone before the method's
/// first await, so reading the target's block ahead of the call tells how much armor was taken down.
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), "LoseBlock")]
public static class AfterBlockStrippedPatch
{
	private static bool Prepare()
	{
		return CaptureGuard.TargetExists(typeof(CreatureCmd), "LoseBlock");
	}

	private static void Prefix(PlayerChoiceContext choiceContext, Creature target, decimal amount, Creature? remover)
	{
		CaptureGuard.Run(delegate
		{
			Capture(choiceContext, target, amount, remover);
		});
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Capture(PlayerChoiceContext choiceContext, Creature target, decimal amount, Creature? remover)
	{
		MeterEvent? meterEvent = GameEventMapper.BlockStripped(choiceContext, target, amount, remover);
		if (meterEvent != null)
		{
			MeterRecorder.Record(meterEvent);
		}
	}
}
