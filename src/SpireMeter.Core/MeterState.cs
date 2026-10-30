using System;
using System.Collections.Generic;
using System.Linq;

namespace SpireMeter.Core;

public sealed class MeterState
{
	private sealed class PlayerStats(PlayerRef player)
	{
		public PlayerRef Player { get; set; } = player;

		public long Damage { get; set; }

		public long Blocked { get; set; }

		/// <summary>Poison ticks credited to this player.</summary>
		public long Poison { get; set; }

		/// <summary>HP removed by Doom kills credited to this player.</summary>
		public long Doom { get; set; }

		public DamageSplit Split => new DamageSplit(Damage - Blocked - Poison - Doom, Blocked, Poison, Doom);

		public long BlockGained { get; set; }

		public long BlockParty { get; set; }

		public long Turns { get; set; }

		public CardTally Cards { get; } = new CardTally();

		public Tally Debuffs { get; } = new Tally();
	}

	private sealed class FightEnemy(EnemyRef enemy)
	{
		public EnemyRef Enemy { get; } = enemy;

		public long Total { get; set; }

		public long Poison { get; set; }

		public Dictionary<ulong, long> ByPlayer { get; } = new Dictionary<ulong, long>();
	}

	private sealed class TurnCounters
	{
		public int Turns { get; set; }

		public long Energy { get; set; }

		public long Played { get; set; }

		public long Drawn { get; set; }

		public long Damage { get; set; }
	}

	private sealed class Tally
	{
		private readonly Dictionary<string, (string Name, long Amount, int Order)> _items = new Dictionary<string, (string, long, int)>();

		public void Add(string key, string name, long amount)
		{
			_items[key] = (_items.TryGetValue(key, out (string, long, int) value) ? (value.Item1, value.Item2 + amount, value.Item3) : (name, amount, _items.Count));
		}

		public IReadOnlyList<NamedAmount> Sorted()
		{
			return (from i in _items.Values
				orderby i.Amount descending, i.Order
				select new NamedAmount(i.Name, i.Amount)).ToList();
		}

		public List<DebuffData> Export()
		{
			return (from p in _items
				orderby p.Value.Order
				select new DebuffData
				{
					Id = p.Key,
					Name = p.Value.Name,
					Amount = p.Value.Amount
				}).ToList();
		}

		public void Load(IEnumerable<DebuffData> debuffs)
		{
			foreach (DebuffData debuff in debuffs)
			{
				_items[debuff.Id] = (debuff.Name, debuff.Amount, _items.Count);
			}
		}
	}

	private sealed class CardTally
	{
		private sealed class Entry(string name, int order)
		{
			public string Name { get; } = name;

			public int Order { get; } = order;

			public long Damage { get; set; }

			public long Poison { get; set; }

			public long Block { get; set; }

			public long BlockParty { get; set; }

			public Tally Debuffs { get; } = new Tally();
		}

		private readonly Dictionary<string, Entry> _items = new Dictionary<string, Entry>();

		public void AddDamage(string? cardId, string? cardName, long amount)
		{
			Get(cardId, cardName).Damage += amount;
		}

		public void AddPoison(string? cardId, string? cardName, long stacks)
		{
			Get(cardId, cardName).Poison += stacks;
		}

		public void AddDebuff(string? cardId, string? cardName, string debuffId, string debuffName, long stacks)
		{
			Get(cardId, cardName).Debuffs.Add(debuffId, debuffName, stacks);
		}

		public void AddBlock(string? cardId, string? cardName, long amount, bool party)
		{
			Entry entry = Get(cardId, cardName);
			entry.Block += amount;
			if (party)
			{
				entry.BlockParty += amount;
			}
		}

		private Entry Get(string? cardId, string? cardName)
		{
			string key = cardId ?? "OTHER";
			if (!_items.TryGetValue(key, out Entry? entry))
			{
				entry = new Entry((cardId == null) ? "Unknown source" : (cardName ?? cardId), _items.Count);
				_items[key] = entry;
			}
			return entry;
		}

		public IReadOnlyList<CardAmount> Sorted()
		{
			return (from p in _items
				let i = p.Value
				orderby i.Damage descending, i.Poison descending, i.Order
				select new CardAmount(i.Name, i.Damage, i.Poison, i.Debuffs.Sorted(), i.Block, i.BlockParty, KindOf(p.Key))).ToList();
		}

		/// <summary>Cards keep their plain id; other sources are saved as "relic:ID", "potion:ID" and so on.</summary>
		private static string KindOf(string key)
		{
			if (key == "OTHER")
			{
				return "other";
			}
			int colon = key.IndexOf(':');
			return (colon > 0) ? key.Substring(0, colon) : "card";
		}

		public List<CardData> Export()
		{
			return (from p in _items
				orderby p.Value.Order
				select new CardData
				{
					Id = p.Key,
					Name = p.Value.Name,
					Damage = p.Value.Damage,
					Poison = p.Value.Poison,
					Block = p.Value.Block,
					BlockParty = p.Value.BlockParty,
					Debuffs = p.Value.Debuffs.Export()
				}).ToList();
		}

		public void Load(IEnumerable<CardData> cards)
		{
			foreach (CardData card in cards)
			{
				Entry entry = new Entry(card.Name, _items.Count)
				{
					Damage = card.Damage,
					Poison = card.Poison,
					Block = card.Block,
					BlockParty = card.BlockParty
				};
				entry.Debuffs.Load(card.Debuffs);
				_items[card.Id] = entry;
			}
		}
	}

	public const string OtherCardId = "OTHER";

	public const string OtherCardName = "Unknown source";

	public const int TurnStatsFights = 5;

	private string? _runId;

	private readonly Dictionary<ulong, PlayerStats> _players = new Dictionary<ulong, PlayerStats>();

	private readonly List<ulong> _playerOrder = new List<ulong>();

	private long _poisonDamage;

	/// <summary>Doom kills with no known applier.</summary>
	private long _doomDamage;

	/// <summary>This fight's damage per player, as <see cref="DamageSplit"/> slots: HP, blocked, poison, doom.</summary>
	private readonly Dictionary<ulong, long[]> _fightSplit = new Dictionary<ulong, long[]>();

	private int _fightsFinished;

	private bool _inCombat;

	private readonly Dictionary<string, FightEnemy> _fightEnemies = new Dictionary<string, FightEnemy>();

	private readonly List<string> _fightEnemyOrder = new List<string>();

	/// <summary>Poison and Doom stacks applied this fight, keyed "poison|enemy id" or "doom|enemy id", by player and card.</summary>
	private readonly Dictionary<string, List<PoisonSource>> _poisonSources = new Dictionary<string, List<PoisonSource>>();

	private sealed class PoisonSource(PlayerRef player, string? cardId, string? cardName)
	{
		public PlayerRef Player { get; } = player;

		public string? CardId { get; } = cardId;

		public string? CardName { get; } = cardName;

		public long Stacks { get; set; }
	}

	private readonly List<Dictionary<ulong, TurnCounters>> _turnFights = new List<Dictionary<ulong, TurnCounters>>();

	private readonly IRunStore? _store;

	/// <summary>
	/// The totals as of the end of the last finished combat. The game only saves between rooms, so after a
	/// reload the interrupted combat is replayed from scratch; its half-counted damage must be dropped.
	/// </summary>
	private RunData? _committed;

	public long Version { get; private set; }

	public MeterState(IRunStore? store = null)
	{
		_store = store;
	}

	public void Apply(MeterEvent e)
	{
		switch (e)
		{
		case RunStarted runStarted:
			if (runStarted.RunId != _runId)
			{
				// A different run: pick up its saved totals if it is a reloaded one, otherwise start from zero.
				_runId = runStarted.RunId;
				_committed = _store?.Load(runStarted.RunId);
			}
			// The same run again means the game reloaded it (or a new fight began): both mean "back to the last finished fight".
			Restore(_committed);
			break;
		case CombatStarted combatStarted:
			_inCombat = true;
			_fightEnemies.Clear();
			_poisonSources.Clear();
			_fightSplit.Clear();
			_fightEnemyOrder.Clear();
			foreach (PlayerRef player in combatStarted.Players)
			{
				GetPlayer(player);
			}
			foreach (EnemyRef enemy in combatStarted.Enemies)
			{
				GetFightEnemy(enemy);
			}
			_turnFights.Add(new Dictionary<ulong, TurnCounters>());
			if (_turnFights.Count > TurnStatsFights)
			{
				_turnFights.RemoveAt(0);
			}
			break;
		case CombatEnded:
			_inCombat = false;
			_fightsFinished++;
			Commit();
			break;
		case DamageDealt damageDealt:
		{
			int unblocked = Math.Max(0, damageDealt.Unblocked);
			int blocked = Math.Max(0, damageDealt.Blocked);
			if (unblocked == 0 && blocked == 0)
			{
				return;
			}
			PlayerStats stats = GetPlayer(damageDealt.Player);
			stats.Damage += unblocked + blocked;
			stats.Blocked += blocked;
			AddTurnDamage(damageDealt.Player, unblocked + blocked);
			AddFightSplit(damageDealt.Player, 0, unblocked);
			AddFightSplit(damageDealt.Player, 1, blocked);
			if (unblocked > 0)
			{
				stats.Cards.AddDamage(damageDealt.CardId, damageDealt.CardName, unblocked);
				FightEnemy fightEnemy = GetFightEnemy(damageDealt.Target);
				fightEnemy.Total += unblocked;
				fightEnemy.ByPlayer[damageDealt.Player.Id] = fightEnemy.ByPlayer.GetValueOrDefault(damageDealt.Player.Id) + unblocked;
			}
			break;
		}
		case BlockStripped blockStripped:
		{
			if (blockStripped.Amount <= 0)
			{
				return;
			}
			// Armor taken down counts like the blocked part of a hit: it is work the party did to the enemy.
			PlayerStats stats = GetPlayer(blockStripped.Player);
			stats.Damage += blockStripped.Amount;
			stats.Blocked += blockStripped.Amount;
			AddTurnDamage(blockStripped.Player, blockStripped.Amount);
			AddFightSplit(blockStripped.Player, 1, blockStripped.Amount);
			stats.Cards.AddDamage(blockStripped.CardId, blockStripped.CardName, blockStripped.Amount);
			break;
		}
		case BlockGained blockGained:
		{
			if (blockGained.Amount <= 0)
			{
				return;
			}
			PlayerStats stats = GetPlayer(blockGained.Giver);
			bool party = blockGained.Receiver.Id != blockGained.Giver.Id;
			stats.BlockGained += blockGained.Amount;
			if (party)
			{
				stats.BlockParty += blockGained.Amount;
			}
			stats.Cards.AddBlock(blockGained.CardId, blockGained.CardName, blockGained.Amount, party);
			break;
		}
		case PoisonDamage poisonDamage:
			if (poisonDamage.Amount <= 0)
			{
				return;
			}
			CreditStacks("poison", poisonDamage.Target, poisonDamage.Amount);
			break;
		case DoomDamage doomDamage:
			if (doomDamage.Amount <= 0)
			{
				return;
			}
			CreditStacks("doom", doomDamage.Target, doomDamage.Amount);
			break;
		case DebuffApplied debuffApplied:
		{
			if (debuffApplied.Stacks <= 0)
			{
				return;
			}
			PlayerStats stats = GetPlayer(debuffApplied.Player);
			stats.Debuffs.Add(debuffApplied.DebuffId, debuffApplied.DebuffName, debuffApplied.Stacks);
			if (debuffApplied.IsPoison)
			{
				stats.Cards.AddPoison(debuffApplied.CardId, debuffApplied.CardName, debuffApplied.Stacks);
				AddStackSource("poison", debuffApplied);
			}
			else
			{
				stats.Cards.AddDebuff(debuffApplied.CardId, debuffApplied.CardName, debuffApplied.DebuffId, debuffApplied.DebuffName, debuffApplied.Stacks);
				if (debuffApplied.IsDoom)
				{
					AddStackSource("doom", debuffApplied);
				}
			}
			break;
		}
		case TurnStarted turnStarted:
		{
			TurnCounters? counters = CurrentTurnCounters(turnStarted.Player);
			if (counters == null)
			{
				return;
			}
			counters.Turns++;
			GetPlayer(turnStarted.Player).Turns++;
			break;
		}
		case EnergySpent energySpent:
		{
			TurnCounters? counters = (energySpent.Amount > 0) ? CurrentTurnCounters(energySpent.Player) : null;
			if (counters == null)
			{
				return;
			}
			counters.Energy += energySpent.Amount;
			break;
		}
		case CardPlayed cardPlayed:
		{
			TurnCounters? counters = CurrentTurnCounters(cardPlayed.Player);
			if (counters == null)
			{
				return;
			}
			counters.Played++;
			break;
		}
		case CardDrawn cardDrawn:
		{
			TurnCounters? counters = CurrentTurnCounters(cardDrawn.Player);
			if (counters == null)
			{
				return;
			}
			counters.Drawn++;
			break;
		}
		default:
			return;
		}
		Version++;
	}

	public MeterSnapshot Snapshot()
	{
		long total = _players.Values.Sum((PlayerStats p) => p.Damage) + _poisonDamage + _doomDamage;
		List<PlayerTotals> players = (from x in _playerOrder.Select((ulong id, int order) => (stats: _players[id], order: order))
			orderby x.stats.Damage descending, x.order
			select new PlayerTotals(x.stats.Player, x.stats.Damage, x.stats.Blocked, (total == 0L) ? 0.0 : ((double)x.stats.Damage / (double)total), x.stats.Cards.Sorted(), x.stats.Debuffs.Sorted(), TurnAveragesFor(x.stats.Player.Id), x.stats.Turns, x.stats.BlockGained, x.stats.BlockParty, x.stats.Split)).ToList();
		List<EnemyFight> fight = (from id in _fightEnemyOrder
			select _fightEnemies[id] into enemy
			select new EnemyFight(enemy.Enemy, enemy.Total + enemy.Poison, (from x in _playerOrder.Select((ulong id, int order) => (id: id, order: order))
				where enemy.ByPlayer.ContainsKey(x.id)
				orderby enemy.ByPlayer[x.id] descending, x.order
				select new PlayerAmount(_players[x.id].Player, enemy.ByPlayer[x.id])).ToList(), enemy.Poison)).ToList();
		List<PlayerSplit> fightPlayers = (from x in _playerOrder.Select((ulong id, int order) => (id: id, order: order))
			where _fightSplit.ContainsKey(x.id)
			let s = _fightSplit[x.id]
			let split = new DamageSplit(s[0], s[1], s[2], s[3])
			orderby split.Total descending, x.order
			select new PlayerSplit(_players[x.id].Player, split)).ToList();
		return new MeterSnapshot(_runId, total, players, _poisonDamage, _inCombat, fight, _fightsFinished, fightPlayers, _doomDamage);
	}

	private TurnAverages TurnAveragesFor(ulong playerId)
	{
		if (_turnFights.Count == 0)
		{
			return TurnAverages.None;
		}
		int num = 0;
		long num2 = 0L;
		long num3 = 0L;
		long num4 = 0L;
		long num5 = 0L;
		foreach (Dictionary<ulong, TurnCounters> turnFight in _turnFights)
		{
			if (turnFight.TryGetValue(playerId, out var value))
			{
				num += value.Turns;
				num2 += value.Energy;
				num3 += value.Played;
				num4 += value.Drawn;
				num5 += value.Damage;
			}
		}
		if (num != 0)
		{
			return new TurnAverages(_turnFights.Count, num, (double)num2 / (double)num, (double)num3 / (double)num, (double)num4 / (double)num, (double)num5 / (double)num);
		}
		return new TurnAverages(_turnFights.Count, 0, 0.0, 0.0, 0.0, 0.0);
	}

	private TurnCounters? CurrentTurnCounters(PlayerRef player)
	{
		if (!_inCombat || _turnFights.Count == 0)
		{
			return null;
		}
		GetPlayer(player);
		List<Dictionary<ulong, TurnCounters>> turnFights = _turnFights;
		Dictionary<ulong, TurnCounters> dictionary = turnFights[turnFights.Count - 1];
		if (!dictionary.TryGetValue(player.Id, out var value))
		{
			value = new TurnCounters();
			dictionary[player.Id] = value;
		}
		return value;
	}

	private void AddStackSource(string kind, DebuffApplied applied)
	{
		string key = kind + "|" + applied.Target.Id;
		if (!_poisonSources.TryGetValue(key, out List<PoisonSource>? sources))
		{
			sources = new List<PoisonSource>();
			_poisonSources[key] = sources;
		}
		PoisonSource? source = sources.Find((PoisonSource s) => s.Player.Id == applied.Player.Id && s.CardId == applied.CardId);
		if (source == null)
		{
			source = new PoisonSource(applied.Player, applied.CardId, applied.CardName);
			sources.Add(source);
		}
		source.Stacks += applied.Stacks;
	}

	/// <summary>
	/// Poison ticks and Doom kills go to whoever applied that power to the enemy this fight, split by their share of
	/// the stacks (largest remainder, so the parts add up exactly). With no known source the damage stays shared.
	/// </summary>
	private void CreditStacks(string kind, EnemyRef target, int amount)
	{
		bool doom = kind == "doom";
		FightEnemy fightEnemy = GetFightEnemy(target);
		if (!_poisonSources.TryGetValue(kind + "|" + target.Id, out List<PoisonSource>? sources) || sources.Sum((PoisonSource s) => s.Stacks) <= 0)
		{
			if (doom)
			{
				_doomDamage += amount;
				fightEnemy.Total += amount;
			}
			else
			{
				_poisonDamage += amount;
				fightEnemy.Poison += amount;
			}
			return;
		}
		long totalStacks = sources.Sum((PoisonSource s) => s.Stacks);
		long[] parts = sources.Select((PoisonSource s) => amount * s.Stacks / totalStacks).ToArray();
		long left = amount - parts.Sum();
		foreach (int i in Enumerable.Range(0, sources.Count).OrderByDescending((int i) => amount * sources[i].Stacks % totalStacks).ThenBy((int i) => i).Take((int)left))
		{
			parts[i]++;
		}
		for (int i = 0; i < sources.Count; i++)
		{
			if (parts[i] <= 0)
			{
				continue;
			}
			PoisonSource source = sources[i];
			PlayerStats stats = GetPlayer(source.Player);
			stats.Damage += parts[i];
			if (doom)
			{
				stats.Doom += parts[i];
			}
			else
			{
				stats.Poison += parts[i];
			}
			AddTurnDamage(source.Player, parts[i]);
			AddFightSplit(source.Player, doom ? 3 : 2, parts[i]);
			stats.Cards.AddDamage(source.CardId, source.CardName, parts[i]);
			fightEnemy.Total += parts[i];
			fightEnemy.ByPlayer[source.Player.Id] = fightEnemy.ByPlayer.GetValueOrDefault(source.Player.Id) + parts[i];
		}
	}

	private void AddFightSplit(PlayerRef player, int slot, long amount)
	{
		if (amount <= 0)
		{
			return;
		}
		GetPlayer(player);
		if (!_fightSplit.TryGetValue(player.Id, out long[]? split))
		{
			split = new long[4];
			_fightSplit[player.Id] = split;
		}
		split[slot] += amount;
	}

	/// <summary>Credits damage to the player's current-fight counters, which feed the damage-per-turn figures.</summary>
	private void AddTurnDamage(PlayerRef player, long amount)
	{
		TurnCounters? counters = CurrentTurnCounters(player);
		if (counters != null)
		{
			counters.Damage += amount;
		}
	}

	private void Commit()
	{
		if (_runId != null)
		{
			_committed = ToData(_runId);
			_store?.Save(_committed);
		}
	}

	private RunData ToData(string runId)
	{
		return new RunData
		{
			RunId = runId,
			SavedAtUtc = DateTime.UtcNow,
			PoisonDamage = _poisonDamage,
			DoomDamage = _doomDamage,
			Fights = _fightsFinished,
			LastFightSplit = _fightSplit.ToDictionary((KeyValuePair<ulong, long[]> s) => s.Key, (KeyValuePair<ulong, long[]> s) => (long[])s.Value.Clone()),
			Players = _playerOrder.Select((ulong id) => _players[id]).Select((PlayerStats p) => new PlayerData
			{
				Id = p.Player.Id,
				Name = p.Player.Name,
				Character = p.Player.Character,
				Damage = p.Damage,
				Blocked = p.Blocked,
				Poison = p.Poison,
				Doom = p.Doom,
				BlockGained = p.BlockGained,
				BlockParty = p.BlockParty,
				Turns = p.Turns,
				Cards = p.Cards.Export(),
				Debuffs = p.Debuffs.Export()
			}).ToList(),
			LastFight = _fightEnemyOrder.Select((string id) => _fightEnemies[id]).Select((FightEnemy f) => new FightEnemyData
			{
				Id = f.Enemy.Id,
				Name = f.Enemy.Name,
				Total = f.Total,
				Poison = f.Poison,
				ByPlayer = new Dictionary<ulong, long>(f.ByPlayer)
			}).ToList(),
			TurnFights = _turnFights.Select((Dictionary<ulong, TurnCounters> fight) => fight.ToDictionary((KeyValuePair<ulong, TurnCounters> t) => t.Key, (KeyValuePair<ulong, TurnCounters> t) => new TurnData
			{
				Turns = t.Value.Turns,
				Energy = t.Value.Energy,
				Played = t.Value.Played,
				Drawn = t.Value.Drawn,
				Damage = t.Value.Damage
			})).ToList()
		};
	}

	/// <summary>Replaces the totals with <paramref name="data"/>, or with nothing when there is none.</summary>
	private void Restore(RunData? data)
	{
		Reset();
		if (data == null)
		{
			return;
		}
		_poisonDamage = data.PoisonDamage;
		_doomDamage = data.DoomDamage;
		_fightsFinished = data.Fights;
		foreach (KeyValuePair<ulong, long[]> split in data.LastFightSplit)
		{
			if (split.Value.Length == 4)
			{
				_fightSplit[split.Key] = (long[])split.Value.Clone();
			}
		}
		foreach (PlayerData saved in data.Players)
		{
			PlayerStats player = GetPlayer(new PlayerRef(saved.Id, saved.Name, saved.Character));
			player.Damage = saved.Damage;
			player.Blocked = saved.Blocked;
			player.Poison = saved.Poison;
			player.Doom = saved.Doom;
			player.BlockGained = saved.BlockGained;
			player.BlockParty = saved.BlockParty;
			player.Turns = saved.Turns;
			player.Cards.Load(saved.Cards);
			player.Debuffs.Load(saved.Debuffs);
		}
		foreach (FightEnemyData saved in data.LastFight)
		{
			FightEnemy enemy = GetFightEnemy(new EnemyRef(saved.Id, saved.Name));
			enemy.Total = saved.Total;
			enemy.Poison = saved.Poison;
			foreach (KeyValuePair<ulong, long> share in saved.ByPlayer)
			{
				enemy.ByPlayer[share.Key] = share.Value;
			}
		}
		foreach (Dictionary<ulong, TurnData> fight in data.TurnFights)
		{
			_turnFights.Add(fight.ToDictionary((KeyValuePair<ulong, TurnData> t) => t.Key, (KeyValuePair<ulong, TurnData> t) => new TurnCounters
			{
				Turns = t.Value.Turns,
				Energy = t.Value.Energy,
				Played = t.Value.Played,
				Drawn = t.Value.Drawn,
				Damage = t.Value.Damage
			}));
		}
	}

	private void Reset()
	{
		_players.Clear();
		_playerOrder.Clear();
		_poisonDamage = 0L;
		_doomDamage = 0L;
		_fightsFinished = 0;
		_inCombat = false;
		_fightEnemies.Clear();
		_poisonSources.Clear();
		_fightSplit.Clear();
		_fightEnemyOrder.Clear();
		_turnFights.Clear();
	}

	private PlayerStats GetPlayer(PlayerRef player)
	{
		if (_players.TryGetValue(player.Id, out PlayerStats value))
		{
			value.Player = player;
			return value;
		}
		value = new PlayerStats(player);
		_players[player.Id] = value;
		_playerOrder.Add(player.Id);
		return value;
	}

	private FightEnemy GetFightEnemy(EnemyRef enemy)
	{
		if (!_fightEnemies.TryGetValue(enemy.Id, out FightEnemy value))
		{
			value = new FightEnemy(enemy);
			_fightEnemies[enemy.Id] = value;
			_fightEnemyOrder.Add(enemy.Id);
		}
		return value;
	}
}
