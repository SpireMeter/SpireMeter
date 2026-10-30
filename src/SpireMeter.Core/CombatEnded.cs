using System.Collections.Generic;

namespace SpireMeter.Core;

public sealed record CombatEnded(IReadOnlyDictionary<ulong, int> GameDamageDealt) : MeterEvent();
