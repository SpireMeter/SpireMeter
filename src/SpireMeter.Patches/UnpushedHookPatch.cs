using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using SpireMeter.Game;

namespace SpireMeter.Patches;

/// <summary>
/// The game runs most hooks with the reacting model pushed on the context, but not the Fool's arrow hooks or
/// "card generated". This marks every model's own version of those hooks while it runs, so Explosive Arrows,
/// Covering Fire, All Is Well, Twine and the like are credited instead of the card that set them off.
/// </summary>
[HarmonyPatch]
public static class UnpushedHookPatch
{
	private static readonly string[] HookNames = new string[4] { "AfterArrowShot", "AfterArrowImpaled", "AfterArrowDislodged", "AfterCardGeneratedForCombat" };

	/// <summary>Harmony asks once for the class, then once per method; only the first answer needs the log line.</summary>
	private static bool Prepare(MethodBase? original)
	{
		if (original != null)
		{
			return true;
		}
		int count = TargetMethods().Count();
		if (count > 0)
		{
			Log.Info($"[SpireMeter] watching {count} arrow and card-generated hooks");
			return true;
		}
		Log.Error("[SpireMeter] no arrow or card-generated hooks found (game updated?); those sources show as their card");
		return false;
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(AbstractModel).Assembly))
		{
			if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters || !typeof(AbstractModel).IsAssignableFrom(type))
			{
				continue;
			}
			foreach (string name in HookNames)
			{
				MethodInfo? method = AccessTools.DeclaredMethod(type, name);
				if (method != null && !method.IsAbstract && method.ReturnType == typeof(Task))
				{
					yield return method;
				}
			}
		}
	}

	private static void Prefix(AbstractModel __instance)
	{
		CaptureGuard.Run(delegate
		{
			SourceTracker.HookEntered(__instance);
		});
	}

	/// <summary>
	/// The hook's task is what the game awaits, so the mark is dropped exactly when the model is done, even if it
	/// failed. A hook that finished on the spot is unmarked right away and its task is left as it is.
	/// </summary>
	private static void Postfix(AbstractModel __instance, ref Task __result)
	{
		if (__result == null || __result.IsCompleted)
		{
			SourceTracker.HookFinished(__instance);
			return;
		}
		__result = Unmark(__result, __instance);
	}

	private static async Task Unmark(Task hook, AbstractModel model)
	{
		try
		{
			await hook;
		}
		finally
		{
			SourceTracker.HookFinished(model);
		}
	}
}
