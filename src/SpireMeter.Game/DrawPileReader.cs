using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SpireMeter.Core;

namespace SpireMeter.Game;

public static class DrawPileReader
{
	private static readonly FieldInfo? LocalPlayerField = AccessTools.Field(typeof(NCombatCardPile), "_localPlayer");

	public static bool Available => (object)LocalPlayerField != null;

	public static DrawOdds.Result? Read(NCombatCardPile button)
	{
		if (LocalPlayerField?.GetValue(button) is Player player)
		{
			PlayerCombatState playerCombatState = player.PlayerCombatState;
			if (playerCombatState != null)
			{
				return DrawOdds.Compute(playerCombatState.DrawPile.Cards.Count, playerCombatState.DiscardPile.Cards.Count);
			}
		}
		return null;
	}
}
