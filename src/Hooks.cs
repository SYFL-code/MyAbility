using CommonUtils.Core;
using HarmonyLib;
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
		#region Camouflage
		HookManager.Register(
				Hook: () => On.PlayerGraphics.Update += Camouflage.PlayerGraphics_Update,
				UnHook: () => On.PlayerGraphics.Update -= Camouflage.PlayerGraphics_Update
			);
			HookManager.Register(
				Hook: () => On.PlayerGraphics.DrawSprites += Camouflage.PlayerGraphics_DrawSprites,
				UnHook: () => On.PlayerGraphics.DrawSprites -= Camouflage.PlayerGraphics_DrawSprites
			);

		Harmony.CreateAndPatchAll(typeof(Camouflage.Patch_VisibilityBonus));
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
				Hook: () => On.Weapon.Update += Penetration.Weapon_Update,
				UnHook: () => On.Weapon.Update -= Penetration.Weapon_Update
			);
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
		else
		{
			return (bool)orig_.DynamicInvoke(weapon, result, eu);
		}
	}

}