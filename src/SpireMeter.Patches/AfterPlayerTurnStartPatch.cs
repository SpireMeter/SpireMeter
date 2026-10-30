using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using SpireMeter.Game;

namespace SpireMeter.Patches;

[HarmonyPatch(typeof(Hook), "AfterPlayerTurnStart")]
public static class AfterPlayerTurnStartPatch
{
	private static bool Prepare()
	{
		return CaptureGuard.TargetExists("AfterPlayerTurnStart");
	}

	private static void Prefix(Player player)
	{
		CaptureGuard.Run(delegate
		{
			Capture(player);
		});
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Capture(Player player)
	{
		MeterRecorder.Record(GameEventMapper.TurnStarted(player));
	}
}
