using System;
using System.Collections.Generic;

namespace SpireMeter.Core;

public sealed record MeterSnapshot(string? RunId, long TotalDamage, IReadOnlyList<PlayerTotals> Players, long PoisonDamage, bool InCombat, IReadOnlyList<EnemyFight> Fight, int FightsFinished, IReadOnlyList<PlayerSplit>? FightPlayers = null, long DoomDamage = 0)
{
	public double PoisonShare => (TotalDamage != 0L) ? ((double)PoisonDamage / (double)TotalDamage) : 0.0;

	public static readonly MeterSnapshot Empty = new MeterSnapshot(null, 0L, Array.Empty<PlayerTotals>(), 0L, InCombat: false, Array.Empty<EnemyFight>(), 0);
}
