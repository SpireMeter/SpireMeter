namespace SpireMeter.Core;

public sealed record DamageDealt(PlayerRef Player, EnemyRef Target, string? CardId, string? CardName, int Unblocked, int Overkill, int Blocked = 0) : MeterEvent();
