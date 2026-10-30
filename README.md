# SpireMeter

A live damage meter for **Slay the Spire 2**, made for co-op. It only reads the game and never changes gameplay or saves. In co-op it counts the whole party, even if you're the only one with the mod.

## Install

1. Download the zip from the [latest release](https://github.com/SpireMeter/SpireMeter/releases/latest).
2. Close the game, unzip the download and double-click **`install.cmd`**.
3. Start the game. If it asks whether to load mods, say yes.

If Windows warns about the script, choose **More info → Run anyway**. Or install by hand: copy the `SpireMeter` folder from the zip into the game's `mods` folder (in Steam: right-click the game → **Manage → Browse local files**).

**Update:** when a new version is out, an **Update** button appears on the meter. Click it and install the new zip the same way. Your settings and numbers are kept.

**Uninstall:** delete `mods\SpireMeter` from the game folder.

**Arrange it:** click **Unlock** on the meter to move, resize and lay it out, then **Lock**.

## Report a bug

Click **Unlock → Report a bug**. The meter saves `bug-report.txt` (your version and this session's meter log, including player names) and opens the bug form on GitHub. Describe what happened, drag the file in, and submit. A screenshot helps.

Ideas are welcome too: [suggest one](https://github.com/SpireMeter/SpireMeter/issues/new?template=feature_request.yml). What changed in each version is in the [changelog](CHANGELOG.md).

## Privacy

At launch the meter asks GitHub once whether a newer version exists. Nothing about you or your game is sent. To turn this off, set `"CheckForUpdates": false` in `%APPDATA%\SlayTheSpire2\SpireMeter\settings.json`.

## For developers

Build with the .NET 9 SDK, pointing `GameDir` at your game folder:

```
dotnet build src/SpireMeter.csproj -c Release -p:GameDir="C:\path\to\Slay the Spire 2"
dotnet run --project tests/SpireMeter.Core.Tests
```

Releases and bug triage: [docs/MAINTAINING.md](docs/MAINTAINING.md). License: [MIT](LICENSE).
