using BepInEx;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace MySlugcat.Patch
{
	public static class MenuPatch
	{
		private static Harmony? _harmony => Plugin.Harmony;

		public static void TryPatch()
		{
			if (_harmony == null)
				return;


			var mmType = AccessTools.TypeByName("MouseDrag.MenuManager");
			if (mmType == null)
			{
				Log.LogWarning("MouseDrag.MenuManager not found, patch skipped");
				return;
			}

			var rawUpdate = AccessTools.Method(mmType, "RawUpdate");
			if (rawUpdate == null)
			{
				Log.LogWarning("MouseDrag.MenuManager.RawUpdate not found");
				return;
			}

			try
			{
				_harmony.Patch(rawUpdate,
					prefix: new HarmonyMethod(AccessTools.Method(
						typeof(MenuPatch), nameof(Prefix_RawUpdate))));
				Log.LogInfo("Patched MouseDrag.MenuManager.RawUpdate");
			}
			catch (Exception ex)
			{
				Log.LogError("Patch RawUpdate failed: " + ex);
			}
		}


		private static bool _wasMenuOpenLastFrame;
		private static int _framesSinceMenuClosed = 999;   // 大值 = 不拦截

		public static bool Prefix_RawUpdate(global::RainWorldGame game)
		{
			try
			{
				EnsureCache();
				bool menuOpen = CritterMenuOpen();

				// 检测菜单关闭的边沿
				if (_wasMenuOpenLastFrame && !menuOpen)
				{
					_framesSinceMenuClosed = 0;
					Log.LogInfo("[CritterCompat] menu closed, blocking MouseDrag for a few frames");
				}
				else if (_framesSinceMenuClosed < 10)
				{
					_framesSinceMenuClosed++;
				}

				_wasMenuOpenLastFrame = menuOpen;

				bool intercept = menuOpen || _framesSinceMenuClosed < 10;
				return !intercept;
			}
			catch (Exception ex)
			{
				Log.LogError("Prefix_RawUpdate: " + ex);
			}
			return true;
		}

		private static bool CritterMenuOpen()
		{
			EnsureCache();
			if (_pluginType == null) return false;

			var inst = _instanceProp?.GetValue(null);
			if (inst == null) return false;
			if (_isMenuOpenF == null) return false;

			return (bool)_isMenuOpenF.GetValue(inst);
		}


		// ---- 缓存 ----
		private static bool _cached;
		private static Type? _pluginType;
		private static PropertyInfo? _instanceProp;

		private static FieldInfo? _isMenuOpenF;

		private static void EnsureCache()
		{
			if (_cached) return;
			try
			{
				var pluginType = AccessTools.TypeByName("Critter.CritterPlugin");
				if (pluginType == null)
				{
					Log.LogWarning("Critter.CritterPlugin not found yet, will retry");
					return;
				}

				var instanceProp = AccessTools.Property(pluginType, "Instance");
				var isMenuOpenF = AccessTools.Field(pluginType, "_isMenuOpen");

				if (instanceProp == null || isMenuOpenF == null)
				{
					Log.LogWarning("Critter fields missing, will retry later");
					return;   // 不置 _cached
				}


				// 全部成功才提交
				_pluginType = pluginType;
				_instanceProp = instanceProp;
				_isMenuOpenF = isMenuOpenF;

				_cached = true;
				Log.LogInfo("MenuPatch cache built");
			}
			catch (Exception ex)
			{
				Log.LogError("EnsureCache failed: " + ex);
			}
		}

	}
}