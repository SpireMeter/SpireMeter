using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SpireMeter.UI;

namespace SpireMeter.Patches;

[HarmonyPatch(typeof(NCombatCardPile), "OnUnfocus")]
public static class DrawPileUnfocusPatch
{
	private static bool Prepare()
	{
		return DrawPileHover.TargetExists("OnUnfocus");
	}

	private static void Postfix(NCombatCardPile __instance)
	{
		CaptureGuard.Run(delegate
		{
			Hide(__instance);
		});
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Hide(NCombatCardPile button)
	{
		if (button is NDrawPileButton)
		{
			DrawOddsPanel.Instance?.Hide();
		}
	}
}
