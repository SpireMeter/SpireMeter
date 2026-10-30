using System.Collections.Generic;

namespace SpireMeter.Core;

/// <summary>
/// What one source did for a player: damage, poison, other debuffs and block. <paramref name="Kind"/> is "card",
/// "relic", "potion", "orb", "power" or "other" (no known source).
/// </summary>
public sealed record CardAmount(string Name, long Damage, long Poison, IReadOnlyList<NamedAmount> Debuffs, long Block = 0, long BlockParty = 0, string Kind = "card")
{
	public bool IsCard => Kind == "card";
}
