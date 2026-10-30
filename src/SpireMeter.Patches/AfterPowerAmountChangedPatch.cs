using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using SpireMeter.Core;
using SpireMeter.Game;

namespace SpireMeter.Patches;

[HarmonyPatch(typeof(Hook), "AfterPowerAmountChanged")]
public static class AfterPowerAmountChangedPatch
{
	private static bool Prepare()
	{
		return CaptureGuard.TargetExists("AfterPowerAmountChanged");
	}

	private static void Prefix(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		CaptureGuard.Run(delegate
		{
			Capture(choiceContext, power, amount, applier, cardSource);
		});
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Capture(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		DebuffApplied debuffApplied = GameEventMapper.DebuffApplied(choiceContext, power, amount, applier, cardSource);
		if ((object)debuffApplied != null)
		{
			MeterRecorder.Record(debuffApplied);
		}
	}
}
