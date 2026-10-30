using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SpireMeter.Core;
using SpireMeter.Game;

namespace SpireMeter.Patches;

[HarmonyPatch(typeof(Hook), "AfterDamageGiven")]
public static class AfterDamageGivenPatch
{
	private static bool Prepare()
	{
		return CaptureGuard.TargetExists("AfterDamageGiven");
	}

	private static void Prefix(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult results, ValueProp props, Creature target, CardModel? cardSource)
	{
		CaptureGuard.Run(delegate
		{
			Capture(choiceContext, dealer, results, props, target, cardSource);
		});
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Capture(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult results, ValueProp props, Creature target, CardModel? cardSource)
	{
		MeterEvent meterEvent = GameEventMapper.Damage(choiceContext, dealer, results, props, target, cardSource);
		if ((object)meterEvent != null)
		{
			MeterRecorder.Record(meterEvent);
		}
	}
}
