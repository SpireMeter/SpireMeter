using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using SpireMeter.Game;
using SpireMeter.UI;

namespace SpireMeter;

[ModInitializer("Initialize")]
public static class ModEntry
{
	public const string ModId = "SpireMeter";

	public const string Version = "1.0.0";

	private static Harmony? _harmony;

	private static MeterWindow? _window;

	public static void Initialize()
	{
		Log.Info("[SpireMeter] loaded v" + Version);
		try
		{
			MeterRecorder.Initialize();
		}
		catch (Exception value)
		{
			Log.Error($"[{"SpireMeter"}] event log setup failed (the meter still works): {value}");
		}
		ApplyPatches();
		if (!(Engine.GetMainLoop() is SceneTree tree))
		{
			Log.Error("[SpireMeter] no SceneTree available, meter window not added");
			return;
		}
		CanvasLayer canvasLayer = Overlay.Attach(tree);
		if (canvasLayer != null)
		{
			_window = new MeterWindow(canvasLayer);
			DrawOddsPanel.Instance = new DrawOddsPanel(canvasLayer);
		}
	}

	private static void ApplyPatches()
	{
		_harmony = new Harmony("spiremeter.tracker");
		List<Type> list = (from t in AccessTools.GetTypesFromAssembly(typeof(ModEntry).Assembly)
			where t.GetCustomAttributes<HarmonyPatch>().Any()
			select t).ToList();
		int num = 0;
		foreach (Type item in list)
		{
			try
			{
				if (_harmony.CreateClassProcessor(item).Patch().Count > 0)
				{
					num++;
				}
			}
			catch (Exception value)
			{
				Log.Error($"[{"SpireMeter"}] patch {item.Name} failed: {value}");
			}
		}
		Log.Info($"[{"SpireMeter"}] patches applied: {num}/{list.Count}");
	}
}
