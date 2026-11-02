namespace SpireMeter.Game;

/// <summary>Poison an enemy takes at the start of its next turn: total damage, how many ticks, and its current stacks.</summary>
public readonly record struct PoisonForecast(int Damage, int Ticks, int Stacks);
