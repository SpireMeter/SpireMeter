using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using SpireMeter.Game;

namespace SpireMeter.Patches;

[HarmonyPatch(typeof(PowerModel), "Flash")]
public static class PowerFlashPatch
{
	private static bool Prepare()
	{
		return CaptureGuard.TargetExists(typeof(PowerModel), "Flash");
	}

	private static void Postfix(PowerModel __instance)
	{
		CaptureGuard.Run(delegate
		{
			SourceTracker.Flashed(__instance);
		});
	}
}
