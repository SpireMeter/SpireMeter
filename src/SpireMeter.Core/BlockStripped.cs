namespace SpireMeter.Core;

/// <summary>A player removed enemy block directly (like Expose does) instead of hitting through it.</summary>
public sealed record BlockStripped(PlayerRef Player, EnemyRef Target, string? CardId, string? CardName, int Amount) : MeterEvent();
