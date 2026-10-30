namespace SpireMeter.Core;

/// <summary>A player's creature gained block; <paramref name="Giver"/> is the card owner, which differs from <paramref name="Receiver"/> when the block went to a teammate.</summary>
public sealed record BlockGained(PlayerRef Giver, PlayerRef Receiver, string? CardId, string? CardName, int Amount) : MeterEvent();
