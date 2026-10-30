namespace SpireMeter.Core;

/// <summary>Doom killed an enemy; the HP it had left counts as damage for whoever applied the Doom.</summary>
public sealed record DoomDamage(EnemyRef Target, int Amount) : MeterEvent();
