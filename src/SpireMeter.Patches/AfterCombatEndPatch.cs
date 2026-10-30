using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SpireMeter.Game;

namespace SpireMeter.Patches;

[HarmonyPatch(typeof(Hook), "AfterCombatEnd")]
public static class AfterCombatEndPatch
{
	private static bool Prepare()
	{
		return CaptureGuard.TargetExists("AfterCombatEnd");
	}

	private static void Prefix(IRunState runState, ICombatState? combatState, CombatRoom room)
	{
		CaptureGuard.Run(delegate
		{
			Capture(runState);
		});
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Capture(IRunState runState)
	{
		MeterRecorder.Record(GameEventMapper.CombatEnded(runState));
	}
}
