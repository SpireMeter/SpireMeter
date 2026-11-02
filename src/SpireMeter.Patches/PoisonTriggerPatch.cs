using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Powers;
using SpireMeter.Game;

namespace SpireMeter.Patches;

[HarmonyPatch(typeof(PoisonPower), "Trigger")]
public static class PoisonTriggerPatch
{
	private static bool Prepare()
	{
		return CaptureGuard.TargetExists(typeof(PoisonPower), "Trigger");
	}

	private static void Prefix(PoisonPower __instance)
	{
		CaptureGuard.Run(delegate
		{
			GameEventMapper.PoisonTriggered(__instance);
		});
	}
}
