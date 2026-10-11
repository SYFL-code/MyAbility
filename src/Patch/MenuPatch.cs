using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MySlugcat.Patch
{
	public static class MenuPatch
	{
		private static Harmony? _harmony => Plugin.Harmony;

		private static Type? _mmType;   // MouseDrag.MenuManager
		private static Type? _rmType;   // Critter.UI.RadialMenu
		private static Type? _rsType;   // Critter.UI.RadialSlot
		private static Type? _hudType;  // Critter.UI.CritterHud
		private static Type? _cpType;   // Critter.CritterPlugin
		public static void TryPatch()
		{
			if (_harmony == null) return;

			FindTypes();   // ← 替换原来的 5 次 TypeByName

			TryPatchRawUpdate();
			On.RainWorld.Update += DeferredPatch;
		}

		private static void FindTypes()
		{
			Assembly? critterAsm = null;
			Assembly? mouseDragAsm = null;

			// 只遍历一次程序集列表（几十项），不再遍历所有类型
			var asms = AppDomain.CurrentDomain.GetAssemblies();
			for (int i = 0; i < asms.Length; i++)
			{
				string name;
				try { name = asms[i].GetName().Name; }
				catch { continue; }

				if (name == "Critter") critterAsm = asms[i];
				else if (name == "MouseDrag") mouseDragAsm = asms[i];

				if (critterAsm != null && mouseDragAsm != null) break;
			}

			if (critterAsm != null)
			{
				_cpType = critterAsm.GetType("Critter.CritterPlugin");
				_rmType = critterAsm.GetType("Critter.UI.RadialMenu");
				_rsType = critterAsm.GetType("Critter.UI.RadialSlot");
				_hudType = critterAsm.GetType("Critter.UI.CritterHud");
			}

			if (mouseDragAsm != null)
				_mmType = mouseDragAsm.GetType("MouseDrag.MenuManager");

			// 回退：只有程序集没加载时才走慢路径
			_cpType ??= AccessTools.TypeByName("Critter.CritterPlugin");
			_rmType ??= AccessTools.TypeByName("Critter.UI.RadialMenu");
			_rsType ??= AccessTools.TypeByName("Critter.UI.RadialSlot");
			_hudType ??= AccessTools.TypeByName("Critter.UI.CritterHud");
			_mmType ??= AccessTools.TypeByName("MouseDrag.MenuManager");

			Log.LogInfo($"FindTypes: mm={_mmType != null}, cp={_cpType != null}, " +
						$"rm={_rmType != null}, rs={_rsType != null}, hud={_hudType != null}");
		}

		private static bool _deferredDone;

		private static void DeferredPatch(On.RainWorld.orig_Update orig, RainWorld self)
		{
			orig(self);

			if (_deferredDone) return;
			_deferredDone = true;
			On.RainWorld.Update -= DeferredPatch;

			try
			{
				TryPatchAnimations();
				TryPatchTooltip();
			}
			catch (Exception ex)
			{
				Log.LogError("DeferredPatch failed: " + ex);
			}
		}
		private static void TryPatchRawUpdate()
		{
			if (_harmony == null || _mmType == null)
			{
				Log.LogWarning("MouseDrag.MenuManager not found, patch skipped");
				return;
			}

			var rawUpdate = AccessTools.Method(_mmType, "RawUpdate");
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


		// ---- 动画加速 ----
		private const float LerpAlpha = 0.5f;   // 原 0.25 -> 0.5（淡入/展开/选中）
		private const float LerpPos = 0.7f;   // 原 0.35 -> 0.7（滑出位置）

		public static void TryPatchAnimations()
		{
			if (_harmony == null) return;
			if (_rmType == null || _rsType == null)
			{
				Log.LogWarning("Critter.UI types not found, animation patch skipped");
				return;
			}
			TryPatchLerps(_rmType, "Update");
			TryPatchLerps(_rsType, "UpdateMesh");
		}


		private static void TryPatchLerps(Type type, string methodName)
		{
			if (_harmony == null)
				return;


			var method = AccessTools.Method(type, methodName);
			if (method == null)
			{
				Log.LogWarning($"{type.Name}.{methodName} not found");
				return;
			}

			try
			{
				_harmony.Patch(method,
					transpiler: new HarmonyMethod(AccessTools.Method(typeof(MenuPatch), nameof(Transpiler_AccelerateLerps)))
					);
				Log.LogInfo($"Patched {type.Name}.{methodName} (lerp transpiler)");
			}
			catch (Exception ex)
			{
				Log.LogError($"Patch {type.Name}.{methodName} failed: " + ex);
			}
		}

		public static IEnumerable<CodeInstruction> Transpiler_AccelerateLerps(
			IEnumerable<CodeInstruction> instructions)
		{
			var codes = new List<CodeInstruction>(instructions);
			int n25 = 0, n35 = 0;

			for (int i = 0; i < codes.Count; i++)
			{
				if (codes[i].opcode != OpCodes.Ldc_R4) continue;
				if (codes[i].operand is not float f) continue;

				if (f == 0.25f)
				{
					codes[i].operand = LerpAlpha;
					n25++;
				}
				else if (f == 0.35f)
				{
					codes[i].operand = LerpPos;
					n35++;
				}
			}

			Log.LogInfo($"Accelerated: 0.25 x{n25} -> {LerpAlpha}, 0.35 x{n35} -> {LerpPos}");
			return codes;
		}


		// ---- 提示框置顶 ----
		public static void TryPatchTooltip()
		{
			if (_harmony == null) return;
			if (_hudType == null)
			{
				Log.LogWarning("CritterHud not found, tooltip patch skipped");
				return;
			}

			var method = AccessTools.Method(_hudType, "UpdateTooltip");
			if (method == null)
			{
				Log.LogWarning("CritterHud.UpdateTooltip not found");
				return;
			}

			try
			{
				_harmony.Patch(method,
					postfix: new HarmonyMethod(AccessTools.Method(
						typeof(MenuPatch), nameof(Postfix_UpdateTooltip))));
				Log.LogInfo("Patched CritterHud.UpdateTooltip");
			}
			catch (Exception ex)
			{
				Log.LogError("Patch UpdateTooltip failed: " + ex);
			}
		}


		private static bool _tooltipWarmed;

		public static void Postfix_UpdateTooltip(object __instance)
		{
			try
			{
				EnsureTooltipCache();
				if (__instance == null) return;

				if (!_tooltipWarmed)
				{
					_tooltipWarmed = true;

					// 顺便把 _sprites 的 FieldInfo 也建好，避免下一帧再反射
					var b = _tooltipBoxF?.GetValue(__instance);
					if (b != null)
						_vanillaBoxSpritesF = AccessTools.Field(b.GetType(), "_sprites");

					return;   // 这一帧不做事，只做 JIT 预热
				}

				var box = _tooltipBoxF?.GetValue(__instance);
				var label = _tooltipLabelF?.GetValue(__instance) as FLabel;

				try { MoveVanillaBoxToFront(box); } catch { }
				try { label?.MoveToFront(); } catch { }
			}
			catch { }
		}

		private static FieldInfo? _tooltipBoxF;
		private static FieldInfo? _tooltipLabelF;

		private static void EnsureTooltipCache()
		{
			if (_tooltipBoxF != null && _tooltipLabelF != null) return;
			if (_hudType == null) return;
			_tooltipBoxF = AccessTools.Field(_hudType, "_tooltipBox");
			_tooltipLabelF = AccessTools.Field(_hudType, "_tooltipLabel");
		}

		private static FieldInfo? _vanillaBoxSpritesF;

		private static void MoveVanillaBoxToFront(object? box)
		{
			if (box == null) return;

			// 只反射查一次 _sprites 字段
			if (_vanillaBoxSpritesF == null)
			{
				_vanillaBoxSpritesF = AccessTools.Field(box.GetType(), "_sprites");
				if (_vanillaBoxSpritesF == null) return;
			}

			if (_vanillaBoxSpritesF.GetValue(box) is not Array sprites) return;

			for (int i = 0; i < sprites.Length; i++)
				if (sprites.GetValue(i) is FNode sprite)
					sprite.MoveToFront();
		}


		// ---- 菜单关闭后拦截鼠标拖拽 ----
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
			if (_cpType == null) return;

			try
			{
				var instanceProp = AccessTools.Property(_cpType, "Instance");
				var isMenuOpenF = AccessTools.Field(_cpType, "_isMenuOpen");

				if (instanceProp == null || isMenuOpenF == null)
				{
					Log.LogWarning("Critter fields missing, will retry later");
					return;
				}

				_pluginType = _cpType;
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