using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SpireMeter.Core;

namespace SpireMeter.Game;

public static class GameEventMapper
{
	private static readonly ConditionalWeakTable<IRunState, string> RunIds = new ConditionalWeakTable<IRunState, string>();

	private static int _runCounter;

	private static readonly FieldInfo? StartTimeField = AccessTools.Field(typeof(RunManager), "_startTime");

	private static bool _fallbackLogged;

	/// <summary>
	/// A run is identified by its seed plus the moment it was created. The game keeps that moment in the run save,
	/// so a run that is reloaded after a crash or a save-and-quit gets the same id back, while a fresh run, even
	/// on the same seed, gets a new one.
	/// </summary>
	public static RunStarted RunStarted(IRunState runState)
	{
		long startTime = ReadStartTime();
		if (startTime > 0)
		{
			return new RunStarted($"{runState.Rng.StringSeed}@{startTime}");
		}
		if (!_fallbackLogged)
		{
			_fallbackLogged = true;
			Log.Warn("[SpireMeter] run start time unavailable (game updated?); runs will not resume after a restart");
		}
		return new RunStarted(RunIds.GetValue(runState, (IRunState rs) => $"{rs.Rng.StringSeed}#{++_runCounter}"));
	}

	private static long ReadStartTime()
	{
		try
		{
			return (StartTimeField?.GetValue(RunManager.Instance) as long?) ?? 0;
		}
		catch (Exception)
		{
			return 0L;
		}
	}

	public static CombatStarted CombatStarted(ICombatState combatState)
	{
		// Anything left on the tracker from out of combat would mislabel the first block of the fight.
		SourceTracker.Clear();
		return new CombatStarted(combatState.Players.Select(ToPlayerRef).ToList(), combatState.Enemies.Select(ToEnemyRef).ToList());
	}

	public static CombatEnded CombatEnded(IRunState runState)
	{
		return new CombatEnded(runState.Players.ToDictionary((Player p) => p.NetId, (Player p) => p.ExtraFields.DamageDealt));
	}

	public static MeterEvent? Damage(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult results, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (target.Side != CombatSide.Enemy)
		{
			return null;
		}
		if (IsPoisonTick(choiceContext, dealer, props, target, cardSource))
		{
			return new PoisonDamage(ToEnemyRef(target), results.UnblockedDamage);
		}
		Source source = FindSource(cardSource, choiceContext.LastInvolvedModel);
		Player? player = CreditedPlayer(dealer, cardSource) ?? source.Owner;
		if (player == null)
		{
			return null;
		}
		source = source.For(player);
		return new DamageDealt(ToPlayerRef(player), ToEnemyRef(target), source.Id, source.Name, results.UnblockedDamage, results.OverkillDamage, results.BlockedDamage);
	}

	public static BlockStripped? BlockStripped(PlayerChoiceContext choiceContext, Creature target, decimal amount, Creature? remover)
	{
		if ((int)target.Side != 2 || target.IsDead || amount <= 0m || CombatManager.Instance.IsOverOrEnding)
		{
			return null;
		}
		Source source = FindSource(null, choiceContext.LastInvolvedModel);
		Player? val = CreditedPlayer(remover, null) ?? source.Owner;
		int num = Math.Min(target.Block, (int)Math.Min(amount, 2147483647m));
		if (val == null || num <= 0)
		{
			return null;
		}
		source = source.For(val);
		return new BlockStripped(ToPlayerRef(val), ToEnemyRef(target), source.Id, source.Name, num);
	}

	public static BlockGained? BlockGained(Creature creature, decimal amount, CardModel? cardSource)
	{
		Player? receiver = creature.Player ?? creature.PetOwner;
		// Block hooks carry no choice context, so a relic or power behind the block is read from the tracker.
		Source source = FindSource(cardSource, SourceTracker.Current);
		Player? giver = source.Owner ?? receiver;
		if (receiver == null || giver == null || amount <= 0m)
		{
			return null;
		}
		return new BlockGained(ToPlayerRef(giver), ToPlayerRef(receiver), source.Id, source.Name, (int)Math.Min(amount, 2147483647m));
	}

	public static DebuffApplied? DebuffApplied(PlayerChoiceContext? choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (amount == 0m || power.GetTypeForAmount(amount) != PowerType.Debuff || power is ITemporaryPower || power.Owner.Side != CombatSide.Enemy || applier == power.Owner)
		{
			return null;
		}
		Source source = FindSource(cardSource, choiceContext?.LastInvolvedModel ?? SourceTracker.Current);
		Player? player = CreditedPlayer(applier, cardSource) ?? source.Owner;
		if (player == null)
		{
			return null;
		}
		source = source.For(player);
		return new DebuffApplied(ToPlayerRef(player), ToEnemyRef(power.Owner), power.Id.ToString(), power.Title.GetFormattedText(), (int)Math.Min(Math.Abs(amount), 2147483647m), source.Id, source.Name, power is PoisonPower, power is DoomPower);
	}

	/// <summary>Doom kills outright instead of dealing damage; the HP the enemy still had is what Doom took.</summary>
	public static DoomDamage? DoomKilled(Creature creature)
	{
		if (creature.Side != CombatSide.Enemy || creature.IsDead || creature.CurrentHp <= 0)
		{
			return null;
		}
		return new DoomDamage(ToEnemyRef(creature), creature.CurrentHp);
	}

	/// <summary>What caused an effect: a card, or a relic, potion, orb or power (id prefixed with its kind).</summary>
	private readonly record struct Source(string? Id, string? Name, Player? Owner)
	{
		/// <summary>A source owned by someone else than the credited player is not theirs to show, so drop it.</summary>
		public Source For(Player player)
		{
			return (Owner == null || Owner == player) ? this : default(Source);
		}
	}

	private static Source FindSource(CardModel? card, AbstractModel? model)
	{
		try
		{
			if (card != null)
			{
				return new Source(card.Id.ToString(), card.TitleLocString.GetFormattedText(), card.Owner);
			}
			switch (model)
			{
			case CardModel c:
				return new Source(c.Id.ToString(), c.TitleLocString.GetFormattedText(), c.Owner);
			case RelicModel r:
				return new Source("relic:" + r.Id, r.Title.GetFormattedText(), r.Owner);
			case PotionModel p:
				return new Source("potion:" + p.Id, p.Title.GetFormattedText(), p.Owner);
			case OrbModel o:
				return new Source("orb:" + o.Id, o.Title.GetFormattedText(), o.Owner);
			case PowerModel w:
			{
				// The party's own powers (Thorns, Envenom...) are theirs; a debuff sitting on an enemy (Strangle)
				// belongs to whoever applied it. Any other enemy power is not a source.
				Player? owner = w.Owner?.Player ?? w.Owner?.PetOwner ?? w.Applier?.Player ?? w.Applier?.PetOwner;
				return (owner == null) ? default(Source) : new Source("power:" + w.Id, w.Title.GetFormattedText(), owner);
			}
			}
		}
		catch (Exception)
		{
		}
		return default(Source);
	}

	public static TurnStarted TurnStarted(Player player)
	{
		return new TurnStarted(ToPlayerRef(player));
	}

	public static EnergySpent? EnergySpent(CardModel card, int amount)
	{
		if (amount <= 0)
		{
			return null;
		}
		return new EnergySpent(ToPlayerRef(card.Owner), amount);
	}

	public static CardPlayed? CardPlayed(CardPlay cardPlay)
	{
		if (cardPlay.IsAutoPlay || !cardPlay.IsFirstInSeries)
		{
			return null;
		}
		return new CardPlayed(ToPlayerRef(cardPlay.Player));
	}

	public static CardDrawn CardDrawn(CardModel card)
	{
		return new CardDrawn(ToPlayerRef(card.Owner));
	}

	private static bool IsPoisonTick(PlayerChoiceContext choiceContext, Creature? dealer, ValueProp props, Creature target, CardModel? cardSource)
	{
		bool flag = dealer == null && cardSource == null && props.HasFlag(ValueProp.Unblockable) && props.HasFlag(ValueProp.Unpowered);
		if (flag)
		{
			AbstractModel lastInvolvedModel = choiceContext.LastInvolvedModel;
			bool flag2 = ((lastInvolvedModel == null || lastInvolvedModel is PoisonPower) ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			return target.HasPower<PoisonPower>();
		}
		return false;
	}

	private static Player? CreditedPlayer(Creature? source, CardModel? cardSource)
	{
		if (source != null)
		{
			return source.Player ?? source.PetOwner;
		}
		return cardSource?.Owner;
	}

	private static PlayerRef ToPlayerRef(Player player)
	{
		return new PlayerRef(player.NetId, PlayerName(player), player.Character.Title.GetFormattedText());
	}

	/// <summary>The player at this keyboard in co-op, or null in single player (where there is only one anyway).</summary>
	public static ulong? LocalPlayerId()
	{
		try
		{
			if (RunManager.Instance == null || RunManager.Instance.IsSingleplayerOrFakeMultiplayer)
			{
				return null;
			}
			return PlatformUtil.GetLocalPlayerId(RunManager.Instance.NetService.Platform);
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static string PlayerName(Player player)
	{
		try
		{
			if (RunManager.Instance.IsSingleplayerOrFakeMultiplayer)
			{
				PlatformType primaryPlatform = PlatformUtil.PrimaryPlatform;
				return PlatformUtil.GetPlayerNameRaw(primaryPlatform, PlatformUtil.GetLocalPlayerId(primaryPlatform));
			}
			return PlatformUtil.GetPlayerNameRaw(RunManager.Instance.NetService.Platform, player.NetId);
		}
		catch (Exception)
		{
			return player.Creature.Name;
		}
	}

	private static EnemyRef ToEnemyRef(Creature enemy)
	{
		return new EnemyRef(enemy.CombatId?.ToString() ?? ("obj" + RuntimeHelpers.GetHashCode(enemy)), enemy.Name);
	}
}
