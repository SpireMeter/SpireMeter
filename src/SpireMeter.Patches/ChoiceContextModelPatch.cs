using HarmonyLib;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SpireMeter.Game;

namespace SpireMeter.Patches;

[HarmonyPatch(typeof(PlayerChoiceContext), "PushModel")]
public static class ChoiceContextPushModelPatch
{
	private static bool Prepare()
	{
		return CaptureGuard.TargetExists(typeof(PlayerChoiceContext), "PushModel");
	}

	private static void Postfix(AbstractModel model)
	{
		CaptureGuard.Run(delegate
		{
			SourceTracker.Pushed(model);
		});
	}
}

[HarmonyPatch(typeof(PlayerChoiceContext), "PopModel")]
public static class ChoiceContextPopModelPatch
{
	private static bool Prepare()
	{
		return CaptureGuard.TargetExists(typeof(PlayerChoiceContext), "PopModel");
	}

	private static void Postfix(AbstractModel model)
	{
		CaptureGuard.Run(delegate
		{
			SourceTracker.Popped(model);
		});
	}
}
