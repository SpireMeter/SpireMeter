using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;

namespace SpireMeter.Patches;

internal static class CaptureGuard
{
	private static bool _errorLogged;

	public static bool TargetExists(string hookName)
	{
		if ((object)AccessTools.Method(typeof(Hook), hookName) != null)
		{
			return true;
		}
		Log.Error($"[{"SpireMeter"}] Hook.{hookName} not found (game updated?); the meter misses these events");
		return false;
	}

	public static bool TargetExists(Type owner, string methodName)
	{
		if ((object)AccessTools.Method(owner, methodName) != null)
		{
			return true;
		}
		Log.Error($"[{"SpireMeter"}] {owner.Name}.{methodName} not found (game updated?); the meter misses these events");
		return false;
	}

	public static void Run(Action capture)
	{
		try
		{
			capture();
		}
		catch (Exception value)
		{
			if (!_errorLogged)
			{
				_errorLogged = true;
				Log.Error($"[{"SpireMeter"}] meter capture failed (further errors are not logged): {value}");
			}
		}
	}
}
