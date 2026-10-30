using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace SpireMeter.Patches;

[HarmonyPatch]
public static class FirstModdedLaunchCloudSyncPatch
{
	private const string TargetName = "DoCloudSync";

	public const string ResetMarkerFileName = "spiremeter-reset-modded.marker";

	/// <summary>The marker name from when this mod was called VibeNight; still honoured.</summary>
	public const string LegacyResetMarkerFileName = "vibenight-reset-modded.marker";

	private static MethodInfo? FindTarget()
	{
		return AccessTools.Method(typeof(NGame), "DoCloudSync", Type.EmptyTypes);
	}

	private static bool Prepare()
	{
		MethodInfo methodInfo = FindTarget();
		if ((object)methodInfo == null || methodInfo.ReturnType != typeof(Task))
		{
			Log.Error("[SpireMeter] NGame.DoCloudSync() not found (game updated?); first-modded-launch save fix is OFF");
			return false;
		}
		return true;
	}

	private static MethodBase TargetMethod()
	{
		return FindTarget();
	}

	private static bool Prefix(ref Task __result)
	{
		try
		{
			Task task = TryReplaceSync();
			if (task == null)
			{
				return true;
			}
			__result = task;
			return false;
		}
		catch (Exception value)
		{
			Log.Error($"[{"SpireMeter"}] first-modded-launch save fix failed, using the game's own cloud sync: {value}");
			return true;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Task? TryReplaceSync()
	{
		if (!ModManager.UnmoddedSavesWereCopied)
		{
			return null;
		}
		string basePath = UserDataPathProvider.GetAccountScopedBasePath(null);
		string path = basePath + "/" + ResetMarkerFileName;
		string legacyPath = basePath + "/" + LegacyResetMarkerFileName;
		bool flag = FileAccess.FileExists(path) || FileAccess.FileExists(legacyPath);
		if (!flag && CloudHasModdedProfile())
		{
			Log.Info("[SpireMeter] first modded launch on this PC, but Steam Cloud already has a modded profile: using the game's own sync to download it");
			return null;
		}
		if (flag)
		{
			DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
			DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(legacyPath));
		}
		Log.Info($"[{"SpireMeter"}] first modded launch{(flag ? " after reset-modded-saves" : "")}: uploading the copied saves to Steam Cloud instead of syncing down");
		return SaveManager.Instance.OverwriteCloudWithLocal();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool CloudHasModdedProfile()
	{
		if (!(AccessTools.Field(typeof(SaveManager), "_saveStore")?.GetValue(SaveManager.Instance) is CloudSaveStore cloudSaveStore))
		{
			return true;
		}
		if (cloudSaveStore.CloudStore.FileExists(ProfileSaveManager.GetProfileSavePath(true)))
		{
			return true;
		}
		for (int i = 1; i <= 3; i++)
		{
			if (cloudSaveStore.CloudStore.FileExists(ProgressSaveManager.GetProgressPathForProfile(i, true)))
			{
				return true;
			}
		}
		return false;
	}
}
