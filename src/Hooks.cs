using CommonUtils.Core;
using HarmonyLib;
using IL;
using ImprovedInput;
using Menu.Remix;
using Mono.Cecil;
using MonoMod.RuntimeDetour;
using MoreSlugcats;
using MySlugcat.Ability;
using On;
using RewiredConsts;
using RWCustom;
using SlugBase.Features;
using Smoke;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using UnityEngine;
using Watcher;
using static CommonUtils.Core.HookManager;
using static PhysicalObject;
namespace MySlugcat;

public static class Hooks
{
	#region Creatures
	#endregion

	// 注册钩子
	public static void RegisterHooks()
	{
		#region Hooks
		{
			// HitSomething
			HookManager.Register(
				Hook: () => On.Weapon.HitSomething += Weapon_HitSomething,
				UnHook: () => On.Weapon.HitSomething -= Weapon_HitSomething
			);
			HookManager.Register(
				Hook: () => On.Spear.HitSomething += Weapon_HitSomething,
				UnHook: () => On.Spear.HitSomething -= Weapon_HitSomething
			);
			HookManager.Register(
				Hook: () => On.Rock.HitSomething += Weapon_HitSomething,
				UnHook: () => On.Rock.HitSomething -= Weapon_HitSomething
			);
			HookManager.Register(
				Hook: () => On.ScavengerBomb.HitSomething += Weapon_HitSomething,
				UnHook: () => On.ScavengerBomb.HitSomething -= Weapon_HitSomething
			);
			if (ModManager.MSC)
			{
				HookManager.Register(
					Hook: () => On.MoreSlugcats.LillyPuck.HitSomething += Weapon_HitSomething,
					UnHook: () => On.MoreSlugcats.LillyPuck.HitSomething -= Weapon_HitSomething
				);
			}
			if (ModManager.Watcher)
			{
				HookManager.Register(
					Hook: () => On.Boomerang.HitSomething += Weapon_HitSomething,
					UnHook: () => On.Boomerang.HitSomething -= Weapon_HitSomething
				);
			}
		}
		#endregion

		#region ModuleHooks
		{
			HookManager.Register(
				Hook: () => On.Weapon.Update += ModuleHooks.Weapon_Update,
				UnHook: () => On.Weapon.Update -= ModuleHooks.Weapon_Update
			);
			HookManager.Register(
				Hook: () => On.Weapon.Thrown += ModuleHooks.Weapon_Thrown,
				UnHook: () => On.Weapon.Thrown -= ModuleHooks.Weapon_Thrown
			);
			HookManager.Register(
				Hook: () => RegisterHitSomething(ModuleHooks.Weapon_HitSomething),
				UnHook: () => UnregisterHitSomething(ModuleHooks.Weapon_HitSomething)
			);
		}
		#endregion

		#region Deflagration
		{
			HookManager.Register(
				Hook: () => On.Player.Die += Deflagration.Player_Die,
				UnHook: () => On.Player.Die -= Deflagration.Player_Die
			);
			HookManager.Register(
				Hook: () => RegisterHitSomething(Deflagration.Deflagration_HitSomething),
				UnHook: () => UnregisterHitSomething(Deflagration.Deflagration_HitSomething)
			);
		}
		#endregion

		#region Camouflage
		HookManager.Register(
				Hook: () => On.Player.Update += Camouflage.Player_Update,
				UnHook: () => On.Player.Update -= Camouflage.Player_Update
			);
		HookManager.Register(
				Hook: () => On.Player.ThrowObject += Camouflage.Player_ThrowObject,
				UnHook: () => On.Player.ThrowObject -= Camouflage.Player_ThrowObject
			);
			HookManager.Register(
				Hook: () => On.PlayerGraphics.DrawSprites += Camouflage.PlayerGraphics_DrawSprites,
				UnHook: () => On.PlayerGraphics.DrawSprites -= Camouflage.PlayerGraphics_DrawSprites
			);

		//Harmony.CreateAndPatchAll(typeof(Camouflage.Patch_VisibilityBonus));
		//Harmony.Patch(AccessTools.Method("命名空间.类名称.方法名称"), prefix: new HarmonyMethod(typeof(钩子方法的类), nameof(钩子方法的名称)));


		//MethodInfo target = typeof(Player).GetProperty("VisibilityBonus", BindingFlags.Public | BindingFlags.Instance).GetGetMethod();
		//if (target != null)
		//{
		//	var hook = new Hook(
		//		target,
		//		new Func<Func<Player, float>, Player, float>((orig, self) =>
		//		{
		//			// 先执行原始方法（等价于 Prefix + Original）
		//			float result = orig(self);

		//			// Postfix 逻辑
		//			try
		//			{
		//				self.GetModule(out var module);
		//				if (module.CamouflageAbility)
		//				{
		//					float visibility = (ColorHelper.Lerp(module.whitePickUpColor, self.ShortCutColor(), module.whiteCamoColor) * 2f) - 0.8f;
		//					Log.LogInfo($"__result:{result}, visibility:{visibility}");
		//					result = Mathf.Min(result, visibility);
		//				}
		//			}
		//			catch (Exception e)
		//			{
		//				Log.LogError($"[MonoMod] VisibilityBonus 补丁出错: {e.Message}");
		//			}

		//			return result;
		//		})
		//	);
		//}
		#endregion

		#region Frame
		{
			HookManager.Register(
				Hook: () => On.Player.Die += Frame.Player_Die,
				UnHook: () => On.Player.Die -= Frame.Player_Die
			);
			HookManager.Register(
				Hook: () => On.Player.Destroy += Frame.Player_Destroy,
				UnHook: () => On.Player.Destroy -= Frame.Player_Destroy
			);
			HookManager.Register(
				Hook: () => On.Lizard.Bite += Frame.Lizard_Bite,
				UnHook: () => On.Lizard.Bite -= Frame.Lizard_Bite
			);
			HookManager.Register(
				Hook: () => On.Vulture.Carry += Frame.Vulture_Carry,
				UnHook: () => On.Vulture.Carry -= Frame.Vulture_Carry
			);
			HookManager.Register(
				Hook: () => On.Creature.Violence += Frame.Creature_Violence,
				UnHook: () => On.Creature.Violence -= Frame.Creature_Violence
			);
		}
		{
			// HitSomething
			HookManager.Register(
				Hook: () => On.Weapon.HitSomething += Frame.Frame_HitSomething,
				UnHook: () => On.Weapon.HitSomething -= Frame.Frame_HitSomething
			);
			HookManager.Register(
				Hook: () => On.Spear.HitSomething += Frame.Frame_HitSomething,
				UnHook: () => On.Spear.HitSomething -= Frame.Frame_HitSomething
			);
			HookManager.Register(
				Hook: () => On.Rock.HitSomething += Frame.Frame_HitSomething,
				UnHook: () => On.Rock.HitSomething -= Frame.Frame_HitSomething
			);
			HookManager.Register(
				Hook: () => On.ScavengerBomb.HitSomething += Frame.Frame_HitSomething,
				UnHook: () => On.ScavengerBomb.HitSomething -= Frame.Frame_HitSomething
			);
			if (ModManager.MSC)
			{
				HookManager.Register(
					Hook: () => On.MoreSlugcats.LillyPuck.HitSomething += Frame.Frame_HitSomething,
					UnHook: () => On.MoreSlugcats.LillyPuck.HitSomething -= Frame.Frame_HitSomething
				);
			}
			if (ModManager.Watcher)
			{
				HookManager.Register(
					Hook: () => On.Boomerang.HitSomething += Frame.Frame_HitSomething,
					UnHook: () => On.Boomerang.HitSomething -= Frame.Frame_HitSomething
				);
			}
		}
		#endregion

		#region ArcLightning
		{
			HookManager.Register(
				Hook: () => On.Weapon.HitSomething += ArcLightning.ArcLightning_HitSomething,
				UnHook: () => On.Weapon.HitSomething -= ArcLightning.ArcLightning_HitSomething
			);
			HookManager.Register(
				Hook: () => On.Spear.HitSomething += ArcLightning.ArcLightning_HitSomething,
				UnHook: () => On.Spear.HitSomething -= ArcLightning.ArcLightning_HitSomething
			);
			HookManager.Register(
				Hook: () => On.Rock.HitSomething += ArcLightning.ArcLightning_HitSomething,
				UnHook: () => On.Rock.HitSomething -= ArcLightning.ArcLightning_HitSomething
			);
		}
		#endregion

		#region Penetration
		{
			HookManager.Register(
				Hook: () => On.Weapon.Thrown += Penetration.Weapon_Thrown,
				UnHook: () => On.Weapon.Thrown -= Penetration.Weapon_Thrown
			);
			HookManager.Register(
				Hook: () => On.Weapon.HitAnotherThrownWeapon += Penetration.Weapon_HitAnotherThrownWeapon,
				UnHook: () => On.Weapon.HitAnotherThrownWeapon -= Penetration.Weapon_HitAnotherThrownWeapon
			);
		}
		{
			// HitSomething
			HookManager.Register(
				Hook: () => On.Weapon.HitSomething += Penetration.PenetrateHit,
				UnHook: () => On.Weapon.HitSomething -= Penetration.PenetrateHit
			);
			HookManager.Register(
				Hook: () => On.Spear.HitSomething += Penetration.PenetrateHit,
				UnHook: () => On.Spear.HitSomething -= Penetration.PenetrateHit
			);
			HookManager.Register(
				Hook: () => On.Rock.HitSomething += Penetration.PenetrateHit,
				UnHook: () => On.Rock.HitSomething -= Penetration.PenetrateHit
			);
			HookManager.Register(
				Hook: () => On.ScavengerBomb.HitSomething += Penetration.PenetrateHit,
				UnHook: () => On.ScavengerBomb.HitSomething -= Penetration.PenetrateHit
			);
			if (ModManager.MSC)
			{
				HookManager.Register(
					Hook: () => On.MoreSlugcats.LillyPuck.HitSomething += Penetration.PenetrateHit,
					UnHook: () => On.MoreSlugcats.LillyPuck.HitSomething -= Penetration.PenetrateHit
				);
			}
			if (ModManager.Watcher)
			{
				HookManager.Register(
					Hook: () => On.Boomerang.HitSomething += Penetration.PenetrateHit,
					UnHook: () => On.Boomerang.HitSomething -= Penetration.PenetrateHit
				);
			}
		}
		#endregion


		try
		{
			//Scrap.Zname.OnEnable();

			// 创建同一个 Harmony 实例，一次性 Patch 所有类
			//Harmony harmony = new Harmony("com.test.id");
			// 这会自动扫描当前程序集，并按优先级排序所有补丁
			//harmony.PatchAll();
		}
		catch (Exception ex)
		{
			Log.LogError($"Error registering hooks: {ex}");
		}
	}

	private static readonly ConditionalWeakTable<Delegate, Func<Weapon, SharedPhysics.CollisionResult, bool, bool>> _origCache = new();
	private static List<Func<Delegate, Weapon, SharedPhysics.CollisionResult, bool, bool>> _hitSomethingHandlers = [];
	public static void RegisterHitSomething(Func<Delegate, Weapon, SharedPhysics.CollisionResult, bool, bool> handler)
	{
		_hitSomethingHandlers.Add(handler);
	}
	public static void UnregisterHitSomething(Func<Delegate, Weapon, SharedPhysics.CollisionResult, bool, bool> handler)
	{
		_hitSomethingHandlers.Remove(handler);
	}

	// HitSomething 钩子入口（LIFO）
	public static bool Weapon_HitSomething<O, W>(O orig_, W weapon, SharedPhysics.CollisionResult result, bool eu)
		where O : Delegate
		where W : Weapon
	{
		Func<W, SharedPhysics.CollisionResult, bool, bool> orig = Invoke_HitSomething<O, W>(orig_);
		if (!_origCache.TryGetValue(orig_, out Func<Weapon, SharedPhysics.CollisionResult, bool, bool> cached))
		{
			for (int i = 0; i < _hitSomethingHandlers.Count; i++)
			{
				int index = i;
				var prev = orig;
				orig = (weapon_, result_, eu_) => _hitSomethingHandlers[index](prev, weapon_, result_, eu_);
			}
		}
		else
		{
			orig = cached;
		}

		return orig(weapon, result, eu);
	}
	public static bool orig_HitSomething<O, W>(O orig_, W weapon, SharedPhysics.CollisionResult result, bool eu)
		where O : Delegate
		where W : Weapon
	{
		if (orig_ is On.Weapon.orig_HitSomething weapon_orig)
		{
			return weapon_orig(weapon, result, eu);
		}
		else if (orig_ is On.Spear.orig_HitSomething spear_orig && weapon is Spear spear)
		{
			return spear_orig(spear, result, eu);
		}
		else if (orig_ is On.Rock.orig_HitSomething rock_orig && weapon is Rock rock)
		{
			return rock_orig(rock, result, eu);
		}
		else if (orig_ is On.ScavengerBomb.orig_HitSomething bomb_orig && weapon is ScavengerBomb bomb)
		{
			return bomb_orig(bomb, result, eu);
		}
		else if (ModManager.MSC && orig_ is On.MoreSlugcats.LillyPuck.orig_HitSomething lillyPuck_orig && weapon is LillyPuck lillyPuck)
		{
			return lillyPuck_orig(lillyPuck, result, eu);
		}
		else if (ModManager.Watcher && orig_ is On.Boomerang.orig_HitSomething boomerang_orig && weapon is Boomerang boomerang)
		{
			return boomerang_orig(boomerang, result, eu);
		}
		else if (orig_ is Func<W, SharedPhysics.CollisionResult, bool, bool> func)
		{
			return func(weapon, result, eu);
		}
		else
		{
			return (bool)orig_.DynamicInvoke(weapon, result, eu);
		}
	}
	public static Func<W, SharedPhysics.CollisionResult, bool, bool> Invoke_HitSomething<O, W>(O orig_)
		where O : Delegate
		where W : Weapon
	{
		if (orig_ is On.Weapon.orig_HitSomething weapon_orig)
		{
			return weapon_orig.Invoke;
		}
		else if (orig_ is On.Spear.orig_HitSomething spear_orig)
		{
			return (w, r, e) => spear_orig((Spear)(object)w, r, e);
		}
		else if (orig_ is On.Rock.orig_HitSomething rock_orig)
		{
			return (w, r, e) => rock_orig((Rock)(object)w, r, e);
		}
		else if (orig_ is On.ScavengerBomb.orig_HitSomething bomb_orig)
		{
			return (w, r, e) => bomb_orig((ScavengerBomb)(object)w, r, e);
		}
		else if (ModManager.MSC && orig_ is On.MoreSlugcats.LillyPuck.orig_HitSomething lillyPuck_orig)
		{
			return (w, r, e) => lillyPuck_orig((LillyPuck)(object)w, r, e);
		}
		else if (ModManager.Watcher && orig_ is On.Boomerang.orig_HitSomething boomerang_orig)
		{
			return (w, r, e) => boomerang_orig((Boomerang)(object)w, r, e);
		}
		else if (orig_ is Func<W, SharedPhysics.CollisionResult, bool, bool> func)
		{
			return func;
		}
		else
		{
			return (w, r, e) => (bool)orig_.DynamicInvoke(w, r, e);
		}
	}

}