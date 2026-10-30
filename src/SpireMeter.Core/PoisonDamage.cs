namespace SpireMeter.Core;

public sealed record PoisonDamage(EnemyRef Target, int Amount) : MeterEvent();
