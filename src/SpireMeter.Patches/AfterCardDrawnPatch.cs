using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using SpireMeter.Game;

namespace SpireMeter.Patches;

[HarmonyPatch(typeof(Hook), "AfterCardDrawn")]
public static class AfterCardDrawnPatch
{
	private static bool Prepare()
	{
		return CaptureGuard.TargetExists("AfterCardDrawn");
	}

	private static void Prefix(CardModel card)
	{
		CaptureGuard.Run(delegate
		{
			Capture(card);
		});
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Capture(CardModel card)
	{
		MeterRecorder.Record(GameEventMapper.CardDrawn(card));
	}
}
