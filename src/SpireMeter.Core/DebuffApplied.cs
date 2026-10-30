namespace SpireMeter.Core;

public sealed record DebuffApplied(PlayerRef Player, EnemyRef Target, string DebuffId, string DebuffName, int Stacks, string? CardId = null, string? CardName = null, bool IsPoison = false, bool IsDoom = false) : MeterEvent();
