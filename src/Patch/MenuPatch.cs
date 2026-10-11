using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
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
					prefix: new HarmonyMethod(AccessTools.Method(typeof(MenuPatch), nameof(Prefix_RawUpdate)))
					);
				Log.LogInfo("Patched MouseDrag.MenuManager.RawUpdate");
			}
			catch (Exception ex)
			{
				Log.LogError("Patch RawUpdate failed: " + ex);
			}

			TryPatchAnimations();
			TryPatchTooltip();
		}


		// ---- 动画加速 ----
		private const float LerpAlpha = 0.5f;   // 原 0.25 -> 0.5（淡入/展开/选中）
		private const float LerpPos = 0.7f;   // 原 0.35 -> 0.7（滑出位置）

		public static void TryPatchAnimations()
		{
			if (_harmony == null) return;

			var radialMenu = AccessTools.TypeByName("Critter.UI.RadialMenu");
			var radialSlot = AccessTools.TypeByName("Critter.UI.RadialSlot");

			if (radialMenu == null || radialSlot == null)
			{
				Log.LogWarning("Critter.UI types not found, animation patch skipped");
				return;
			}

			TryPatchLerps(radialMenu, "Update");
			TryPatchLerps(radialSlot, "UpdateMesh");
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

			var hudType = AccessTools.TypeByName("Critter.UI.CritterHud");
			if (hudType == null)
			{
				Log.LogWarning("CritterHud not found, tooltip patch skipped");
				return;
			}

			var method = AccessTools.Method(hudType, "UpdateTooltip");
			if (method == null)
			{
				Log.LogWarning("CritterHud.UpdateTooltip not found");
				return;
			}

			try
			{
				_harmony.Patch(method,
					postfix: new HarmonyMethod(AccessTools.Method(typeof(MenuPatch), nameof(Postfix_UpdateTooltip)))
					);
				Log.LogInfo("Patched CritterHud.UpdateTooltip");
			}
			catch (Exception ex)
			{
				Log.LogError("Patch UpdateTooltip failed: " + ex);
			}
		}


		public static void Postfix_UpdateTooltip(object __instance)
		{
			try
			{
				EnsureTooltipCache();
				if (__instance == null) return;

				var box = _tooltipBoxF?.GetValue(__instance);
				var label = _tooltipLabelF?.GetValue(__instance) as FLabel;

				if (label != null && box != null && label.isVisible && !string.IsNullOrEmpty(label.text))
				{

				}

				try { MoveVanillaBoxToFront(box); } catch { }
				try { label?.MoveToFront(); } catch { }
			}
			catch { }
		}

		private static FieldInfo? _tooltipBoxSpritesF;
		private static MethodInfo? _tooltipBoxDrawM;
		private static FieldInfo? _tooltipBoxF;
		private static FieldInfo? _tooltipLabelF;

		private static void EnsureTooltipCache()
		{
			if (_tooltipBoxF != null && _tooltipLabelF != null) return;
			var hudType = AccessTools.TypeByName("Critter.UI.CritterHud");
			if (hudType == null) return;
			_tooltipBoxF = AccessTools.Field(hudType, "_tooltipBox");
			_tooltipLabelF = AccessTools.Field(hudType, "_tooltipLabel");
		}

		private static void MoveVanillaBoxToFront(object? box)
		{
			if (box == null) return;

			var spritesField = AccessTools.Field(box.GetType(), "_sprites");
			if (spritesField == null) return;

			var sprites = spritesField.GetValue(box) as Array;
			if (sprites == null) return;

			// ---- 修复主填充与侧填充的亚像素缝隙 ----
			// 关键：扩边量要 ≥ 2px，保证 overlap 至少覆盖 1 个完整像素，
			// 不会因为「扩边后边缘又落在 .5 上」而重新贴边。
			//if (sprites.Length > 8 && sprites.GetValue(8) is FSprite mainFill)
			//{
			//	const float Expand = 2f;
			//	mainFill.x -= Expand;
			//	mainFill.y -= Expand;
			//	mainFill.scaleX += Expand * 2f;
			//	mainFill.scaleY += Expand * 2f;
			//}

			// 顺序遍历，把每个 sprite 移到父容器最末，保持内部绘制顺序
			for (int i = 0; i < sprites.Length; i++)
			{
				if (sprites.GetValue(i) is FNode sprite)
					sprite.MoveToFront();
			}
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