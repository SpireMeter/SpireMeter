namespace SpireMeter.Core;

/// <summary>Damage by how it landed: into HP, into block (or armor stripped), by poison, or by a Doom kill.</summary>
public readonly record struct DamageSplit(long Hp, long Blocked, long Poison, long Doom)
{
	public long Total => Hp + Blocked + Poison + Doom;
}

/// <summary>One player's damage this fight, split by type.</summary>
public sealed record PlayerSplit(PlayerRef Player, DamageSplit Split);
