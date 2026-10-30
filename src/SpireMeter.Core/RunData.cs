using System;
using System.Collections.Generic;

namespace SpireMeter.Core;

/// <summary>Everything the meter needs to rebuild its totals for one run, as plain JSON-friendly data.</summary>
public sealed class RunData
{
	public const int CurrentFormat = 1;

	public int Format { get; set; } = 1;

	public string RunId { get; set; } = "";

	public DateTime SavedAtUtc { get; set; }

	public long PoisonDamage { get; set; }

	/// <summary>Doom kills with no known applier.</summary>
	public long DoomDamage { get; set; }

	/// <summary>The last fight per player: HP, blocked, poison, doom.</summary>
	public Dictionary<ulong, long[]> LastFightSplit { get; set; } = new Dictionary<ulong, long[]>();

	/// <summary>How many fights of this run have finished.</summary>
	public int Fights { get; set; }

	/// <summary>In first-seen order, which is the tie-break for equal damage.</summary>
	public List<PlayerData> Players { get; set; } = new List<PlayerData>();

	/// <summary>The last finished fight, so the Fight section is not blank after a reload.</summary>
	public List<FightEnemyData> LastFight { get; set; } = new List<FightEnemyData>();

	/// <summary>Per-fight turn counters for the last few fights, oldest first.</summary>
	public List<Dictionary<ulong, TurnData>> TurnFights { get; set; } = new List<Dictionary<ulong, TurnData>>();
}

public sealed class PlayerData
{
	public ulong Id { get; set; }

	public string Name { get; set; } = "";

	public string Character { get; set; } = "";

	public long Damage { get; set; }

	public long Blocked { get; set; }

	/// <summary>Poison ticks credited to this player.</summary>
	public long Poison { get; set; }

	/// <summary>HP removed by Doom kills credited to this player.</summary>
	public long Doom { get; set; }

	public long BlockGained { get; set; }

	public long BlockParty { get; set; }

	/// <summary>Turns this player has started over the run.</summary>
	public long Turns { get; set; }

	/// <summary>In first-seen order.</summary>
	public List<CardData> Cards { get; set; } = new List<CardData>();

	/// <summary>In first-seen order.</summary>
	public List<DebuffData> Debuffs { get; set; } = new List<DebuffData>();
}

public sealed class CardData
{
	public string Id { get; set; } = "";

	public string Name { get; set; } = "";

	public long Damage { get; set; }

	public long Poison { get; set; }

	public long Block { get; set; }

	public long BlockParty { get; set; }

	/// <summary>Other debuffs this card applied, in first-seen order.</summary>
	public List<DebuffData> Debuffs { get; set; } = new List<DebuffData>();
}

public sealed class DebuffData
{
	public string Id { get; set; } = "";

	public string Name { get; set; } = "";

	public long Amount { get; set; }
}

public sealed class TurnData
{
	public int Turns { get; set; }

	public long Energy { get; set; }

	public long Played { get; set; }

	public long Drawn { get; set; }

	public long Damage { get; set; }
}

/// <summary>One enemy of the last finished fight, with what each player did to it.</summary>
public sealed class FightEnemyData
{
	public string Id { get; set; } = "";

	public string Name { get; set; } = "";

	public long Total { get; set; }

	public long Poison { get; set; }

	public Dictionary<ulong, long> ByPlayer { get; set; } = new Dictionary<ulong, long>();
}
