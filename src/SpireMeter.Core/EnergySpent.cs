namespace SpireMeter.Core;

public sealed record EnergySpent(PlayerRef Player, int Amount) : MeterEvent();
