using System.Collections.Generic;

namespace SpireMeter.Core;

public sealed record PlayerTotals(PlayerRef Player, long Damage, long Blocked, double Share, IReadOnlyList<CardAmount> Cards, IReadOnlyList<NamedAmount> Debuffs, TurnAverages Turns, long TurnsPlayed, long BlockGained = 0, long BlockParty = 0, DamageSplit Split = default)
{
	public long HpDamage => Damage - Blocked;

	/// <summary>Average damage per turn over the whole run.</summary>
	public double DamagePerTurn => (TurnsPlayed == 0L) ? 0.0 : ((double)Damage / (double)TurnsPlayed);
}
