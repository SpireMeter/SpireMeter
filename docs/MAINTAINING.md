# Maintaining SpireMeter

## Handling bug reports

Reports arrive through the **Bug report** form, mostly via the in-game **Report a bug** button, so they carry the version and a `bug-report.txt`.

1. **Triage within a few days.** Check the version: if it's older than the latest release, ask them to update and retry first.
2. **Reproduce from the log.** `bug-report.txt` ends with the session's event log, one JSON line per event (`DamageDealt`, `PoisonDamage`, `DebuffApplied`, `BlockGained`...). For a wrong damage number, find the fight (`CombatStarted`) and add up the events for that enemy; compare with the HP numbers in the report.
3. **Label it:**
   - `bug` (set by the form): not looked at yet
   - `confirmed`: reproduced or clearly understood from the log
   - `needs info`: waiting on the reporter; close after two weeks of silence
   - `game update`: broke because Slay the Spire 2 changed
   - `idea` (set by the idea form): a suggestion
4. **Fix.** Where the fix is in `src/SpireMeter.Core`, add a check for it in `tests/SpireMeter.Core.Tests/Program.cs`.
5. **Close it** by writing "Fixes #N" in the fix's commit message (GitHub closes the issue when it's pushed), and list the fix in the changelog.

When a game update lands, start the game with the mod and look in the game's log for `[SpireMeter] patches applied: X/Y`. A missing patch means a game method moved; the meter keeps running without it, but those numbers stop counting.

## Making a release

1. Bump the version in **both** `src/SpireMeter.json` and `src/SpireMeter/ModEntry.cs` (`Version`). Use `1.0.1` for fixes and `1.1.0` for new features.
2. Add a section at the top of `CHANGELOG.md`.
3. Build the zip. This runs the core checks and refuses to package if the two versions disagree:

   ```
   powershell -ExecutionPolicy Bypass -File tools/make-release.ps1
   ```

4. Install `dist\SpireMeter` in your own game and play a fight.
5. Commit, tag and publish (the tag must be `v` plus the version; the in-game update check reads it):

   ```
   git commit -am "SpireMeter 1.0.1"
   git tag v1.0.1
   git push origin main --tags
   gh release create v1.0.1 release/SpireMeter-1.0.1.zip --title "SpireMeter 1.0.1" --notes-file <the new changelog section>
   ```

Everyone on an older version sees the **Update** button the next time they start the game.

## Repository layout

| Path | What |
|---|---|
| `src/` | The mod. `SpireMeter.Core` is game-free logic (state, totals, saving); `Game` maps game objects to meter events; `Patches` hooks the game; `UI` draws the window |
| `tests/SpireMeter.Core.Tests` | Console checks for the core: `dotnet run --project tests/SpireMeter.Core.Tests` |
| `tools/make-release.ps1` | Builds `release/SpireMeter-<version>.zip` |
| `tools/installer/` | `install.cmd`, `install.ps1` and the readme that go into the zip |
