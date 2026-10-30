using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SpireMeter.Game;
using SpireMeter.UI;

namespace SpireMeter.Patches;

[HarmonyPatch(typeof(NCombatCardPile), "OnFocus")]
public static class DrawPileFocusPatch
{
	private static bool Prepare()
	{
		return DrawPileHover.TargetExists("OnFocus");
	}

	private static void Postfix(NCombatCardPile __instance)
	{
		CaptureGuard.Run(delegate
		{
			Show(__instance);
		});
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Show(NCombatCardPile button)
	{
		if (button is NDrawPileButton)
		{
			DrawOddsPanel.Instance?.Show(button, () => DrawPileReader.Read(button));
		}
	}
}
