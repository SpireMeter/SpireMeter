namespace SpireMeter.Core;

/// <param name="Resumed">The game loaded this run from a save rather than starting it fresh.</param>
public sealed record RunStarted(string RunId, bool Resumed = false) : MeterEvent();
