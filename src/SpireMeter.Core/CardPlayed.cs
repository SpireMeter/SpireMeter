namespace SpireMeter.Core;

public sealed record CardPlayed(PlayerRef Player) : MeterEvent();
