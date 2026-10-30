using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Runs;
using SpireMeter.Game;

namespace SpireMeter.Patches;

[HarmonyPatch(typeof(Hook), "BeforeCombatStart")]
public static class BeforeCombatStartPatch
{
	private static bool Prepare()
	{
		return CaptureGuard.TargetExists("BeforeCombatStart");
	}

	private static void Prefix(IRunState runState, ICombatState? combatState)
	{
		CaptureGuard.Run(delegate
		{
			Capture(runState, combatState);
		});
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Capture(IRunState runState, ICombatState? combatState)
	{
		MeterRecorder.Record(GameEventMapper.RunStarted(runState));
		if (combatState != null)
		{
			MeterRecorder.Record(GameEventMapper.CombatStarted(combatState));
		}
	}
}
