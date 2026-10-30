using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SpireMeter.Game;

namespace SpireMeter.Patches;

internal static class DrawPileHover
{
	public static bool TargetExists(string method)
	{
		if ((object)AccessTools.Method(typeof(NCombatCardPile), method) != null && DrawPileReader.Available)
		{
			return true;
		}
		Log.Error($"[{"SpireMeter"}] NCombatCardPile.{method} or _localPlayer not found (game updated?); no topdeck chance on hover");
		return false;
	}
}
