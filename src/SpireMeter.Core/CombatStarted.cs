using System.Collections.Generic;

namespace SpireMeter.Core;

public sealed record CombatStarted(IReadOnlyList<PlayerRef> Players, IReadOnlyList<EnemyRef> Enemies) : MeterEvent();
