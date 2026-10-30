using System.Globalization;

namespace SpireMeter.Core;

public static class DrawOdds
{
	public sealed record Result(double? Chance, int Cards, bool FromDiscard, string Text);

	public static Result Compute(int drawPileCount, int discardPileCount)
	{
		if (drawPileCount > 0)
		{
			double num = 1.0 / (double)drawPileCount;
			return new Result(num, drawPileCount, FromDiscard: false, $"Topdeck chance per card: {Percent(num)} ({drawPileCount} {Cards(drawPileCount)})");
		}
		if (discardPileCount > 0)
		{
			double num2 = 1.0 / (double)discardPileCount;
			return new Result(num2, discardPileCount, FromDiscard: true, $"Draw pile empty: next draw reshuffles {discardPileCount} {Cards(discardPileCount)}, {Percent(num2)} per card");
		}
		return new Result(null, 0, FromDiscard: false, "No cards to draw");
	}

	private static string Percent(double chance)
	{
		return (chance * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + "%";
	}

	private static string Cards(int n)
	{
		if (n != 1)
		{
			return "cards";
		}
		return "card";
	}
}
