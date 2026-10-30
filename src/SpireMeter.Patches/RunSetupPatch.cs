using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;
using SpireMeter.Game;

namespace SpireMeter.Patches;

/// <summary>
/// Every way of starting a run (new or loaded, solo or co-op) goes through RunManager.InitializeShared once the
/// run's start time is known. Hooking it resets the meter the moment a new run begins and brings a reloaded run's
/// totals back before its first fight, instead of waiting for that fight to start.
/// </summary>
[HarmonyPatch(typeof(RunManager), "InitializeShared")]
public static class RunSetupPatch
{
	private static readonly System.Reflection.MethodInfo? StateGetter = AccessTools.PropertyGetter(typeof(RunManager), "State");

	private static bool Prepare()
	{
		if (StateGetter != null)
		{
			return true;
		}
		Log.Error("[SpireMeter] RunManager.State not found (game updated?); the meter only notices a new run at its first fight");
		return false;
	}

	private static void Postfix(RunManager __instance)
	{
		CaptureGuard.Run(delegate
		{
			Capture(__instance);
		});
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Capture(RunManager runManager)
	{
		if (StateGetter!.Invoke(runManager, null) is IRunState runState)
		{
			MeterRecorder.Record(GameEventMapper.RunStarted(runState));
		}
	}
}
