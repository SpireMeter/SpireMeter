using System;
using System.Collections.Generic;

namespace SpireMeter.Core;

/// <summary>
/// A made-up four-player run that touches every kind of number the meter shows (attacks, blocked hits, Expose-style
/// armor strip, poison, debuffs, block given to teammates, relic damage), so the layout can be checked without a party.
/// It never touches a store, so the real run is left alone.
/// </summary>
public static class DemoRun
{
	public static MeterState Build()
	{
		Random rng = new Random(7);
		MeterState state = new MeterState();
		PlayerRef ironclad = new PlayerRef(1, "Puck", "The Ironclad");
		PlayerRef silent = new PlayerRef(2, "Nightshade", "The Silent");
		PlayerRef defect = new PlayerRef(3, "Sparky", "The Defect");
		PlayerRef regent = new PlayerRef(4, "Rexmanderthegreat", "The Necrobinder");
		PlayerRef[] players = new PlayerRef[4] { ironclad, silent, defect, regent };
		state.Apply(new RunStarted("DEMO"));
		string[][] fights = new string[4][]
		{
			new string[2] { "Nibbit", "Nibbit" },
			new string[1] { "Jaw Worm" },
			new string[3] { "Louse", "Louse", "Cultist" },
			new string[1] { "Lagavulin" }
		};
		for (int f = 0; f < fights.Length; f++)
		{
			List<EnemyRef> enemies = new List<EnemyRef>();
			for (int e = 0; e < fights[f].Length; e++)
			{
				enemies.Add(new EnemyRef($"{f}-{e}", fights[f][e]));
			}
			state.Apply(new CombatStarted(players, enemies));
			for (int turn = 0; turn < 4; turn++)
			{
				foreach (PlayerRef p in players)
				{
					state.Apply(new TurnStarted(p));
					state.Apply(new EnergySpent(p, 3));
					for (int c = 0; c < 4; c++)
					{
						state.Apply(new CardDrawn(p));
					}
					for (int c = 0; c < 3; c++)
					{
						state.Apply(new CardPlayed(p));
					}
				}
				EnemyRef target = enemies[rng.Next(enemies.Count)];
				Hit(state, ironclad, target, "BASH", "Bash", 8 + rng.Next(6), rng.Next(4));
				Hit(state, ironclad, target, "STRIKE_I", "Strike", 6 + rng.Next(4), 0);
				state.Apply(new DebuffApplied(ironclad, target, "VULNERABLE", "Vulnerable", 2, "BASH", "Bash"));
				state.Apply(new BlockGained(ironclad, ironclad, "DEFEND_I", "Defend", 5));
				state.Apply(new BlockGained(ironclad, defect, "SHIELD_ALLY", "Shield Ally", 7));
				Hit(state, silent, target, "DAGGER", "Dagger Throw", 9, 0);
				state.Apply(new DebuffApplied(silent, target, "POISON", "Poison", 5, "DEADLY_POISON", "Deadly Poison", IsPoison: true));
				state.Apply(new DebuffApplied(silent, target, "WEAK", "Weak", 1, "NEUTRALIZE", "Neutralize"));
				state.Apply(new PoisonDamage(target, 4 + turn));
				state.Apply(new BlockGained(silent, silent, "SURVIVOR", "Survivor", 8));
				Hit(state, defect, target, "BALL_LIGHTNING", "Ball Lightning", 7 + rng.Next(5), 0);
				Hit(state, defect, target, "orb:LIGHTNING", "Lightning", 3, 0);
				state.Apply(new BlockStripped(defect, target, "EXPOSE", "Expose", rng.Next(3, 9)));
				Hit(state, regent, target, "REAP", "Reap", 12 + rng.Next(10), rng.Next(6));
				state.Apply(new BlockGained(regent, ironclad, "BONE_SHIELD", "Bone Shield", 6));
				state.Apply(new BlockGained(regent, regent, "relic:ORICHALCUM", "Orichalcum", 3));
				state.Apply(new DebuffApplied(regent, target, "DOOM", "Doom", 6, "SCOURGE", "Scourge", IsDoom: true));
				if (turn == 3)
				{
					state.Apply(new DoomDamage(target, 18));
				}
				Hit(state, ironclad, target, "power:THORNS", "Thorns", 3, 0);
				state.Apply(new DebuffApplied(ironclad, target, "WEAK", "Weak", 1, "UPPERCUT", "Uppercut"));
				state.Apply(new DebuffApplied(ironclad, target, "VULNERABLE", "Vulnerable", 1, "UPPERCUT", "Uppercut"));
				if (turn == 0)
				{
					Hit(state, silent, target, "potion:FIRE_POTION", "Fire Potion", 20, 0);
					Hit(state, regent, target, null, null, 2, 0);
				}
			}
			// The last fight stays open, like being mid-combat.
			if (f < fights.Length - 1)
			{
				state.Apply(new CombatEnded(new Dictionary<ulong, int>()));
			}
		}
		return state;
	}

	private static void Hit(MeterState state, PlayerRef player, EnemyRef target, string? cardId, string? cardName, int damage, int blocked)
	{
		state.Apply(new DamageDealt(player, target, cardId, cardName, damage, 0, blocked));
	}
}
