using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using SpireMeter.Core;
using SpireMeter.Game;

namespace SpireMeter.Patches;

[HarmonyPatch(typeof(Hook), "AfterCardPlayed")]
public static class AfterCardPlayedPatch
{
	private static bool Prepare()
	{
		return CaptureGuard.TargetExists("AfterCardPlayed");
	}

	private static void Prefix(CardPlay cardPlay)
	{
		CaptureGuard.Run(delegate
		{
			Capture(cardPlay);
		});
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Capture(CardPlay cardPlay)
	{
		CardPlayed cardPlayed = GameEventMapper.CardPlayed(cardPlay);
		if ((object)cardPlayed != null)
		{
			MeterRecorder.Record(cardPlayed);
		}
	}
}
