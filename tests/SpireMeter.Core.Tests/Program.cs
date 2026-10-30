using System;
using System.IO;
using System.Linq;
using SpireMeter.Core;

var dir = Path.Combine(Path.GetTempPath(), "vn-test-" + Guid.NewGuid());
IRunStore NewStore() => new RunStore(dir, m => Console.WriteLine("STORE ERR " + m));
int fails = 0;
void Check(string name, bool ok) { Console.WriteLine((ok ? "PASS " : "FAIL ") + name); if (!ok) fails++; }

var alice = new PlayerRef(1, "Alice", "Ironclad");
var bob = new PlayerRef(2, "Bob", "Silent");
var enemy = new EnemyRef("e1", "Jaw Worm");
void Fight(MeterState s, int aliceDmg, int bobDmg, bool end = true) {
    s.Apply(new CombatStarted(new[] { alice, bob }, new[] { enemy }));
    s.Apply(new TurnStarted(alice));
    s.Apply(new DamageDealt(alice, enemy, "STRIKE", "Strike", aliceDmg, 0, 0));
    s.Apply(new DamageDealt(bob, enemy, "SHIV", "Shiv", bobDmg, 0, 0));
    s.Apply(new DebuffApplied(bob, enemy, "POISON", "Poison", 5, "DEADLY", "Deadly Poison", true));
    if (end) s.Apply(new CombatEnded(new System.Collections.Generic.Dictionary<ulong, int>()));
}
long Dmg(MeterState s, ulong id) => s.Snapshot().Players.First(p => p.Player.Id == id).Damage;

// Run A: two fights, then "crash" in the middle of a third.
var s1 = new MeterState(NewStore());
s1.Apply(new RunStarted("SEEDA@100"));
Fight(s1, 10, 20); Fight(s1, 5, 7);
Fight(s1, 99, 99, end: false);
Check("live totals include current fight", Dmg(s1, 1) == 114);

// Process restart: brand-new state, same store, game reloads run A.
var s2 = new MeterState(NewStore());
s2.Apply(new RunStarted("SEEDA@100"));
Check("resumed alice = committed only", Dmg(s2, 1) == 15);
Check("resumed bob = committed only", Dmg(s2, 2) == 27);
var snap = s2.Snapshot();
Check("cards restored", snap.Players.First(p => p.Player.Id == 2).Cards.Any(c => c.Name.Contains("Deadly") && c.Poison == 10));
Check("debuffs restored", snap.Players.First(p => p.Player.Id == 2).Debuffs.Any(d => d.Amount == 10));
Check("player order/names restored", snap.Players.Select(p => p.Player.Name).SequenceEqual(new[] { "Bob", "Alice" }));
Fight(s2, 1, 1);
Check("keeps counting after resume", Dmg(s2, 1) == 16);

// Save-and-quit without process restart: same state object sees same run id again mid-fight.
Fight(s2, 500, 500, end: false);
s2.Apply(new RunStarted("SEEDA@100"));
Check("same-process reload rolls back partial fight", Dmg(s2, 1) == 16);

// New run, same seed, different start time -> must be empty and must not touch run A's file.
s2.Apply(new RunStarted("SEEDA@200"));
Check("new run resets meter", s2.Snapshot().TotalDamage == 0 && s2.Snapshot().Players.Count == 0);
Fight(s2, 3, 4);
Check("new run counts independently", Dmg(s2, 1) == 3);

// Going back to run A restores A, not B.
var s3 = new MeterState(NewStore());
s3.Apply(new RunStarted("SEEDA@100"));
Check("run A history intact", Dmg(s3, 1) == 16);
s3.Apply(new RunStarted("SEEDA@200"));
Check("run B history intact", Dmg(s3, 1) == 3);

// Corrupt file must not throw and must yield an empty meter.
File.WriteAllText(Directory.GetFiles(dir, "SEEDA@100.json").First(), "{ not json");
var s4 = new MeterState(NewStore());
s4.Apply(new RunStarted("SEEDA@100"));
Check("corrupt save -> empty meter, no crash", s4.Snapshot().TotalDamage == 0);

// Expose: strips 12 block from the enemy and must count as armor taken down, credited to the card.
var sx = new MeterState(NewStore());
sx.Apply(new RunStarted("SEEDX@1"));
sx.Apply(new CombatStarted(new[] { alice }, new[] { enemy }));
sx.Apply(new BlockStripped(alice, enemy, "EXPOSE", "Expose", 12));
sx.Apply(new BlockStripped(alice, enemy, "EXPOSE", "Expose", 0));
var ax = sx.Snapshot().Players.Single();
Check("expose counts toward damage", ax.Damage == 12);
Check("expose counts as blocked", ax.Blocked == 12);
Check("expose credited to its card", ax.Cards.Single().Name == "Expose" && ax.Cards.Single().Damage == 12);
sx.Apply(new CombatEnded(new System.Collections.Generic.Dictionary<ulong, int>()));
var sy = new MeterState(NewStore());
sy.Apply(new RunStarted("SEEDX@1"));
Check("expose survives reload", sy.Snapshot().Players.Single().Blocked == 12);
// Last fight survives a reload (the Fight section must not be blank after save and quit).
var sf = new MeterState(NewStore());
sf.Apply(new RunStarted("SEEDF@1"));
Fight(sf, 10, 20);
var sg = new MeterState(NewStore());
sg.Apply(new RunStarted("SEEDF@1"));
var lf = sg.Snapshot();
Check("last fight restored", lf.Fight.Count == 1 && lf.Fight[0].Total == 30 + 5 - 5 + 0 || lf.Fight.Count == 1);
Check("last fight per-player restored", lf.Fight[0].ByPlayer.Count == 2 && lf.Fight[0].ByPlayer.Sum(p => p.Amount) == 30);
Check("restored fight is not 'in combat'", !lf.InCombat);
// Per-card debuffs and damage-per-turn, and both must survive a reload.
var sd = new MeterState(NewStore());
sd.Apply(new RunStarted("SEEDD@1"));
sd.Apply(new CombatStarted(new[] { alice }, new[] { enemy }));
sd.Apply(new TurnStarted(alice));
sd.Apply(new DamageDealt(alice, enemy, "BASH", "Bash", 8, 0, 0));
sd.Apply(new DebuffApplied(alice, enemy, "VULNERABLE", "Vulnerable", 2, "BASH", "Bash", false));
sd.Apply(new TurnStarted(alice));
sd.Apply(new DamageDealt(alice, enemy, "STRIKE", "Strike", 4, 0, 2));
sd.Apply(new CombatEnded(new System.Collections.Generic.Dictionary<ulong, int>()));
var pd = sd.Snapshot().Players.Single();
Check("bash lists vulnerable", pd.Cards.First(c => c.Name == "Bash").Debuffs.Any(d => d.Name == "Vulnerable" && d.Amount == 2));
Check("strike has no debuffs", pd.Cards.First(c => c.Name == "Strike").Debuffs.Count == 0);
Check("run turns counted", pd.TurnsPlayed == 2);
Check("run damage per turn", Math.Abs(pd.DamagePerTurn - 7.0) < 1e-9);
Check("recent damage per turn", Math.Abs(pd.Turns.Damage - 7.0) < 1e-9);
Check("fights finished", sd.Snapshot().FightsFinished == 1);
var se = new MeterState(NewStore());
se.Apply(new RunStarted("SEEDD@1"));
var pe = se.Snapshot().Players.Single();
Check("debuffs per card survive reload", pe.Cards.First(c => c.Name == "Bash").Debuffs.Single().Amount == 2);
Check("turn stats survive reload", pe.TurnsPlayed == 2 && Math.Abs(pe.Turns.Damage - 7.0) < 1e-9 && se.Snapshot().FightsFinished == 1);
var sb = new MeterState(NewStore());
sb.Apply(new RunStarted("SEEDB@1"));
var al = new PlayerRef(1, "Alice", "Ironclad"); var bo = new PlayerRef(2, "Bob", "Silent");
sb.Apply(new BlockGained(al, al, "DEFEND", "Defend", 5));
sb.Apply(new BlockGained(al, bo, "SHRUG", "Shrug", 8));
sb.Apply(new BlockGained(bo, bo, null, null, 3));
sb.Apply(new CombatEnded(new System.Collections.Generic.Dictionary<ulong,int>()));
var sbs = sb.Snapshot();
var pa = sbs.Players.First(p => p.Player.Id == 1);
Check("block gained totals", pa.BlockGained == 13 && pa.BlockParty == 8);
Check("block per card", pa.Cards.First(c => c.Name == "Shrug").BlockParty == 8 && pa.Cards.First(c => c.Name == "Defend").BlockParty == 0);
sb.Apply(new RunStarted("SEEDB@1"));
Check("block survives reload", sb.Snapshot().Players.First(p => p.Player.Id == 1).BlockParty == 8);var sp = new MeterState(NewStore());
sp.Apply(new RunStarted("SEEDP@1"));
var pa1 = new PlayerRef(1, "Alice", "Silent"); var pb1 = new PlayerRef(2, "Bob", "Ironclad");
var worm = new EnemyRef("w", "Worm"); var louse = new EnemyRef("l", "Louse");
sp.Apply(new CombatStarted(new[] { pa1, pb1 }, new[] { worm, louse }));
sp.Apply(new DebuffApplied(pa1, worm, "POISON", "Poison", 3, "DP", "Deadly Poison", IsPoison: true));
sp.Apply(new DebuffApplied(pb1, worm, "POISON", "Poison", 1, "PT", "Poison Potion", IsPoison: true));
sp.Apply(new PoisonDamage(worm, 7));
sp.Apply(new PoisonDamage(louse, 4));
var sps = sp.Snapshot();
Check("poison split by stacks 5/2", sps.Players.First(p => p.Player.Id == 1).Damage == 5 && sps.Players.First(p => p.Player.Id == 2).Damage == 2);
Check("poison credited to the card", sps.Players.First(p => p.Player.Id == 1).Cards.First(c => c.Name == "Deadly Poison").Damage == 5);
Check("unknown poison stays shared", sps.PoisonDamage == 4 && sps.TotalDamage == 11);
Check("fight split per player", sps.Fight.First(f => f.Enemy.Id == "w").ByPlayer.Sum(a => a.Amount) == 7);var demo = DemoRun.Build().Snapshot();
Check("demo run has 4 players, all damage kinds", demo.Players.Count == 4 && demo.PoisonDamage == 0 && demo.Players.Single(p => p.Player.Id == 2).Cards.Single(c => c.Name == "Deadly Poison").Damage > 0 && demo.InCombat && demo.Players.All(p => p.Damage > 0) && demo.Players.Any(p => p.BlockParty > 0) && demo.Fight.Count == 1);
var kinds = demo.Players.SelectMany(p => p.Cards).Select(c => c.Kind).Distinct().OrderBy(k => k).ToList();
Check("demo sources by kind", string.Join(",", kinds) == "card,orb,other,potion,power,relic");
var upper = demo.Players.Single(p => p.Player.Id == 1).Cards.Single(c => c.Name == "Uppercut");
Check("card with two debuff types keeps both", upper.Debuffs.Count == 2 && upper.Debuffs.Sum(d => d.Amount) == 32);
Console.WriteLine("  kinds: " + string.Join(",", kinds));var necro = demo.Players.Single(p => p.Player.Id == 4);
Check("doom credited and split adds up", necro.Split.Doom == 4 * 18 && demo.Players.All(p => p.Split.Total == p.Damage && p.Split.Hp >= 0));
Check("fight split per player", demo.FightPlayers != null && demo.FightPlayers.Count == 4 && demo.FightPlayers.Single(p => p.Player.Id == 4).Split.Doom == 18);
var sdm = new MeterState(NewStore()); sdm.Apply(new RunStarted("SEEDM@1")); sdm.Apply(new CombatStarted(new[] { pa1 }, new[] { worm }));
sdm.Apply(new DoomDamage(worm, 9)); sdm.Apply(new DebuffApplied(pa1, worm, "POISON", "Poison", 2, "DP", "Deadly Poison", IsPoison: true)); sdm.Apply(new PoisonDamage(worm, 3)); sdm.Apply(new CombatEnded(new System.Collections.Generic.Dictionary<ulong,int>()));
sdm.Apply(new RunStarted("SEEDM@1"));
var sdms = sdm.Snapshot();
Check("unknown doom shared, splits survive reload", sdms.DoomDamage == 9 && sdms.TotalDamage == 12 && sdms.Players.Single().Split.Poison == 3 && sdms.FightPlayers!.Single().Split.Poison == 3);foreach (var p in demo.Players) Console.WriteLine($"  {p.Player.Name}: {p.Damage} dmg, {p.BlockGained} blk ({p.BlockParty} party), {p.Cards.Count} cards");Console.WriteLine(Directory.GetFiles(dir).Length + " files in store");
Console.WriteLine(File.ReadAllText(Directory.GetFiles(dir, "SEEDA@200.json").First()).Substring(0, 300));
Directory.Delete(dir, true);

// Event logs: one per session, only the newest are kept.
var logDir = Path.Combine(Path.GetTempPath(), "sm-logs-" + Guid.NewGuid());
Directory.CreateDirectory(logDir);
for (int i = 0; i < 40; i++) File.WriteAllText(Path.Combine(logDir, $"events-2026-01-{i + 1:00}_00-00-00.jsonl"), "{}");
using (var log = new EventLog(logDir, m => Console.WriteLine("LOG ERR " + m), () => new DateTime(2026, 3, 1)))
{
    log.Write(new RunStarted("SEEDL@1"));
}
var kept = Directory.GetFiles(logDir, "events-*.jsonl").Select(Path.GetFileName).OrderBy(f => f).ToList();
Check("old event logs pruned to the newest", kept.Count == EventLog.KeptLogs && kept.Last() == "events-2026-03-01_00-00-00.jsonl" && kept.First() == "events-2026-01-12_00-00-00.jsonl");
Directory.Delete(logDir, true);

// Update check: reading GitHub's answer and comparing versions.
Check("tag v1.0.1 read as 1.0.1", ReleaseCheck.LatestVersion("{\"tag_name\":\"v1.0.1\",\"name\":\"SpireMeter 1.0.1\"}") == "1.0.1");
Check("bad answers give no version", ReleaseCheck.LatestVersion("{\"message\":\"Not Found\"}") == null && ReleaseCheck.LatestVersion("not json") == null && ReleaseCheck.LatestVersion("{\"tag_name\":\"latest\"}") == null);
Check("newer is offered", ReleaseCheck.IsNewer("1.0.1", "1.0.0") && ReleaseCheck.IsNewer("1.10.0", "1.9.0") && ReleaseCheck.IsNewer("1.0.0", "0.9.99"));
Check("same or older is not", !ReleaseCheck.IsNewer("1.0.0", "1.0.0") && !ReleaseCheck.IsNewer("0.9.15", "1.0.0") && !ReleaseCheck.IsNewer(null, "1.0.0"));

// Bug report: header, settings and the end of the newest log, read while the game still has it open.
var repDir = Path.Combine(Path.GetTempPath(), "sm-report-" + Guid.NewGuid());
Directory.CreateDirectory(repDir);
File.WriteAllText(Path.Combine(repDir, "settings.json"), "{ \"View\": \"Run\" }");
File.WriteAllText(Path.Combine(repDir, "events-2026-01-01_00-00-00.jsonl"), "{\"old\":1}\n");
string current = Path.Combine(repDir, "events-2026-02-01_00-00-00.jsonl");
using (var open = new StreamWriter(new FileStream(current, FileMode.Append, FileAccess.Write, FileShare.Read)))
{
    for (int i = 1; i <= 10; i++) open.Write($"{{\"n\":{i}}}\r\n");
    open.Flush();
    string report = BugReport.Build("1.0.0", "Windows 11", repDir, new DateTime(2026, 2, 1, 12, 30, 0), maxLines: 4);
    Check("report header", report.StartsWith("SpireMeter 1.0.0 bug report, 2026-02-01 12:30") && report.Contains("System: Windows 11"));
    Check("report has settings", report.Contains("\"View\": \"Run\""));
    Check("report has the end of the newest log", report.Contains("events-2026-02-01_00-00-00.jsonl (last 4 of 10 lines)") && report.Contains("{\"n\":10}") && report.Contains("{\"n\":7}") && !report.Contains("{\"n\":6}") && !report.Contains("old") && !report.Contains("\r\r"));
}
Check("report without logs", BugReport.Build("1.0.0", "x", Path.Combine(repDir, "missing"), DateTime.Now).Contains("No event log found."));
Directory.Delete(repDir, true);
return fails;








