namespace SpireMeter.Core;

/// <summary>Per-turn averages over the last few fights.</summary>
public sealed record TurnAverages(int Fights, int Turns, double Energy, double Played, double Drawn, double Damage)
{
	public static readonly TurnAverages None = new TurnAverages(0, 0, 0.0, 0.0, 0.0, 0.0);
}
