using System.Collections.Generic;

namespace SpireMeter.Core;

public sealed record EnemyFight(EnemyRef Enemy, long Total, IReadOnlyList<PlayerAmount> ByPlayer, long Poison);
