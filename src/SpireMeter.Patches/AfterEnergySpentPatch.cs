using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using SpireMeter.Core;
using SpireMeter.Game;

namespace SpireMeter.Patches;

[HarmonyPatch(typeof(Hook), "AfterEnergySpent")]
public static class AfterEnergySpentPatch
{
	private static bool Prepare()
	{
		return CaptureGuard.TargetExists("AfterEnergySpent");
	}

	private static void Prefix(CardModel card, int amount)
	{
		CaptureGuard.Run(delegate
		{
			Capture(card, amount);
		});
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Capture(CardModel card, int amount)
	{
		EnergySpent energySpent = GameEventMapper.EnergySpent(card, amount);
		if ((object)energySpent != null)
		{
			MeterRecorder.Record(energySpent);
		}
	}
}
