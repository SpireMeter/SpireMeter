namespace SpireMeter.Core;

/// <param name="Booster">For a tick that only happened because of Accelerant: whose Accelerant it was, and its source.</param>
public sealed record PoisonDamage(EnemyRef Target, int Amount, PlayerRef? Booster = null, string? BoosterId = null, string? BoosterName = null) : MeterEvent();
