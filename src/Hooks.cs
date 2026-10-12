using CommonUtils.Core;
using HarmonyLib;
using IL;
using ImprovedInput;
using Menu.Remix;
using Mono.Cecil;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.HookGen;
using MoreSlugcats;
using MySlugcat.Ability;
using MySlugcat.Patch;
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
using static CommonUtils.Core.UnifiedHooks;
using static PhysicalObject;
namespace MySlugcat;

public static class Hooks
{
	// 注册钩子
	public static void RegisterHooks()
	{
		#region Creatures
		{

		}
		#endregion



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
				Hook: () => UnifiedSubscribe.HitSomething += ModuleHooks.Weapon_HitSomething,
				UnHook: () => UnifiedSubscribe.HitSomething -= ModuleHooks.Weapon_HitSomething
			);
		}
		#endregion

		#region ExtraGrasp
		{
			HookManager.Register(
				Hook: () => On.Player.ctor += ExtraGrasp.Player_ctor,
				UnHook: () => On.Player.ctor -= ExtraGrasp.Player_ctor
			);
			HookManager.Register(
				Hook: () => On.PlayerGraphics.ctor += ExtraGrasp.PlayerGraphics_ctor,
				UnHook: () => On.PlayerGraphics.ctor -= ExtraGrasp.PlayerGraphics_ctor
			);
			HookManager.Register(
				Hook: () => On.Creature.SwitchGrasps += ExtraGrasp.Creature_SwitchGrasps,
				UnHook: () => On.Creature.SwitchGrasps -= ExtraGrasp.Creature_SwitchGrasps
			);
			HookManager.Register(
				Hook: () => IL.Player.SpitUpCraftedObject += ExtraGrasp.IL_Player_SpitUpCraftedObject,
				UnHook: () => IL.Player.SpitUpCraftedObject -= ExtraGrasp.IL_Player_SpitUpCraftedObject
			);
			HookManager.Register(
				Hook: () => IL.PlayerGraphics.Update += ExtraGrasp.IL_PlayerGraphics_Update,
				UnHook: () => IL.PlayerGraphics.Update -= ExtraGrasp.IL_PlayerGraphics_Update
			);
			HookManager.Register(
				Hook: () => On.Player.GrabUpdate += ExtraGrasp.Player_GrabUpdate,
				UnHook: () => On.Player.GrabUpdate -= ExtraGrasp.Player_GrabUpdate
			);
			HookManager.Register(
				Hook: () => IL.Player.GrabUpdate += ExtraGrasp.IL_Player_GrabUpdate,
				UnHook: () => IL.Player.GrabUpdate -= ExtraGrasp.IL_Player_GrabUpdate
			);
			HookManager.Register(
				Hook: () => On.PlayerGraphics.ThrowObject += ExtraGrasp.PlayerGraphics_ThrowObject,
				UnHook: () => On.PlayerGraphics.ThrowObject -= ExtraGrasp.PlayerGraphics_ThrowObject
			);
			HookManager.Register(
				Hook: () => On.Player.CanIPickThisUp += ExtraGrasp.Player_CanIPickThisUp,
				UnHook: () => On.Player.CanIPickThisUp -= ExtraGrasp.Player_CanIPickThisUp
			);
			HookManager.Register(
				Hook: () => On.Player.GraphicsModuleUpdated += ExtraGrasp.GraphicsModuleUpdated,
				UnHook: () => On.Player.GraphicsModuleUpdated -= ExtraGrasp.GraphicsModuleUpdated
			);
			HookManager.Register(
				Hook: () => On.Player.GetHeldItemDirection += ExtraGrasp.GetHeldItemDirection,
				UnHook: () => On.Player.GetHeldItemDirection -= ExtraGrasp.GetHeldItemDirection
			);
		}
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
				Hook: () => UnifiedSubscribe.HitSomething += Frame.Frame_HitSomething,
				UnHook: () => UnifiedSubscribe.HitSomething -= Frame.Frame_HitSomething
			);
		}
		#endregion

		#region StalwartScute
		{
			HookManager.Register(
				Hook: () => On.Creature.Update += StalwartScute.Creature_Update,
				UnHook: () => On.Creature.Update -= StalwartScute.Creature_Update
			);
			HookManager.Register(
				Hook: () => On.Lizard.SpearStick += StalwartScute.Lizard_SpearStick,
				UnHook: () => On.Lizard.SpearStick -= StalwartScute.Lizard_SpearStick
			);
			HookManager.Register(
				Hook: () => On.Scavenger.SpearStick += StalwartScute.Scavenger_SpearStick,
				UnHook: () => On.Scavenger.SpearStick -= StalwartScute.Scavenger_SpearStick
			);
			HookManager.Register(
				Hook: () => On.Lizard.Violence += StalwartScute.Lizard_Violence,
				UnHook: () => On.Lizard.Violence -= StalwartScute.Lizard_Violence
			);
			HookManager.Register(
				Hook: () => On.Scavenger.Violence += StalwartScute.Scavenger_Violence,
				UnHook: () => On.Scavenger.Violence -= StalwartScute.Scavenger_Violence
			);
			HookManager.Register(
				Hook: () => On.Lizard.InitiateGraphicsModule += StalwartScute.Lizard_InitiateGraphicsModule,
				UnHook: () => On.Lizard.InitiateGraphicsModule -= StalwartScute.Lizard_InitiateGraphicsModule
			);
			//HookManager.Register(
			//	Hook: () => IL.ScavengerGraphics.ctor += StalwartScute.IL_ScavengerGraphics_ctor,
			//	UnHook: () => IL.ScavengerGraphics.ctor -= StalwartScute.IL_ScavengerGraphics_ctor
			//);
			HookManager.Register(
				Hook: () => On.ScavengerGraphics.InitiateSprites += StalwartScute.ScavengerGraphics_InitiateSprites,
				UnHook: () => On.ScavengerGraphics.InitiateSprites -= StalwartScute.ScavengerGraphics_InitiateSprites
			);
			HookManager.Register(
				Hook: () => On.ScavengerGraphics.DrawSprites += StalwartScute.ScavengerGraphics_DrawSprites,
				UnHook: () => On.ScavengerGraphics.DrawSprites -= StalwartScute.ScavengerGraphics_DrawSprites
			);
			HookManager.Register(
				Hook: () => On.ScavengerGraphics.ApplyPalette += StalwartScute.ScavengerGraphics_ApplyPalette,
				UnHook: () => On.ScavengerGraphics.ApplyPalette -= StalwartScute.ScavengerGraphics_ApplyPalette
			);
			HookManager.Register(
				Hook: () => On.ScavengerGraphics.AddToContainer += StalwartScute.ScavengerGraphics_AddToContainer,
				UnHook: () => On.ScavengerGraphics.AddToContainer -= StalwartScute.ScavengerGraphics_AddToContainer
			);
		}
		#endregion

		#region CrystalShield
		{
			HookManager.Register(
				Hook: () => On.Creature.Update += CrystalShield.Creature_Update,
				UnHook: () => On.Creature.Update -= CrystalShield.Creature_Update
			);
			HookManager.Register(
				Hook: () => UnifiedSubscribe.HitSomething += CrystalShield.CrystalShield_HitSomething,
				UnHook: () => UnifiedSubscribe.HitSomething -= CrystalShield.CrystalShield_HitSomething
			);
			HookManager.Register(
				Hook: () => UnifiedSubscribe.Violence += CrystalShield.Violence,
				UnHook: () => UnifiedSubscribe.Violence -= CrystalShield.Violence
			);
		}
		#endregion

		#region Penetration
		{
			HookManager.Register(
				Hook: () => On.Weapon.HitAnotherThrownWeapon += Penetration.Weapon_HitAnotherThrownWeapon,
				UnHook: () => On.Weapon.HitAnotherThrownWeapon -= Penetration.Weapon_HitAnotherThrownWeapon
			);
			HookManager.Register(
				Hook: () => On.Lizard.HitInMouth += Penetration.HitInMouth,
				UnHook: () => On.Lizard.HitInMouth -= Penetration.HitInMouth
			);
			HookManager.Register(
				Hook: () => On.Weapon.Update += Penetration.Weapon_Update,
				UnHook: () => On.Weapon.Update -= Penetration.Weapon_Update
			);
		}
		{
			// HitSomething
			HookManager.Register(
				Hook: () => UnifiedSubscribe.HitSomething += Penetration.PenetrateHit,
				UnHook: () => UnifiedSubscribe.HitSomething -= Penetration.PenetrateHit
			);
		}
		#endregion

		#region TrackingThrow
		{
			HookManager.Register(
				Hook: () => On.Weapon.Thrown += TrackingThrow.Weapon_Thrown,
				UnHook: () => On.Weapon.Thrown -= TrackingThrow.Weapon_Thrown
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
				Hook: () => UnifiedSubscribe.HitSomething += Deflagration.Deflagration_HitSomething,
				UnHook: () => UnifiedSubscribe.HitSomething -= Deflagration.Deflagration_HitSomething
			);
		}
		#endregion

		#region Camouflage
		{
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
		}
		#endregion

		#region ArcLightning
		{
			HookManager.Register(
				Hook: () => UnifiedSubscribe.HitSomething += ArcLightning.ArcLightning_HitSomething,
				UnHook: () => UnifiedSubscribe.HitSomething -= ArcLightning.ArcLightning_HitSomething
			);
			HookManager.Register(
				Hook: () => On.Lizard.Bite += ArcLightning.Lizard_Bite,
				UnHook: () => On.Lizard.Bite -= ArcLightning.Lizard_Bite
			);
			HookManager.Register(
				Hook: () => On.Creature.Update += ArcLightning.Creature_Update,
				UnHook: () => On.Creature.Update -= ArcLightning.Creature_Update
			);
			HookManager.Register(
				Hook: () => On.Weapon.Thrown += ArcLightning.Weapon_Thrown,
				UnHook: () => On.Weapon.Thrown -= ArcLightning.Weapon_Thrown
			);
		}
		#endregion

		#region CritterPatch
		{
			HookManager.Register(
				Hook: () => CritterPatch.TryPatch(),
				UnHook: null
			);
		}
		#endregion

		#region RemixAutoRestart
		{
			HookManager.Register(
				Hook: () => UpdatableManager.Register(RemixAutoRestart.Instance),
				UnHook: null
			);
		}
		#endregion



		try
		{

		}
		catch (Exception ex)
		{
			Log.LogError($"Error registering hooks: {ex}");
		}
	}



	#region HitSomething
	public static event Func<Delegate, Weapon, SharedPhysics.CollisionResult, bool, bool> Hit0Something
	{
		add
		{
			_hitSomethingHandlers.Add(value);
			Rebuild();
		}
		remove
		{
			_hitSomethingHandlers.Remove(value);
			Rebuild();
		}
	}
	private static List<Func<Delegate, Weapon, SharedPhysics.CollisionResult, bool, bool>> _hitSomethingHandlers = [];
	[ThreadStatic]
	private static Stack<Func<Weapon, SharedPhysics.CollisionResult, bool, bool>> _origStack = new Stack<Func<Weapon, SharedPhysics.CollisionResult, bool, bool>>();
	private static Func<Weapon, SharedPhysics.CollisionResult, bool, bool>? chain = null;
	public static void Rebuild()
	{
		if (_hitSomethingHandlers.Count == 0)
		{
			chain = null;
			return;
		}

		chain = (weapon_, result_, eu_) => _hitSomethingHandlers[0](_origStack.Peek(), weapon_, result_, eu_);
		for (int i = 1; i < _hitSomethingHandlers.Count; i++)
		{
			int index = i;
			var prev = chain;
			chain = (weapon_, result_, eu_) => _hitSomethingHandlers[index](prev, weapon_, result_, eu_);
		}
	}

	// HitSomething 钩子入口（LIFO）
	public static bool Weapon_HitSomething<O, W>(O orig_, W weapon, SharedPhysics.CollisionResult result, bool eu)
		where O : Delegate
		where W : Weapon
	{
		_origStack ??= new Stack<Func<Weapon, SharedPhysics.CollisionResult, bool, bool>>();
		_origStack.Push(Invoke_HitSomething<O, Weapon>(orig_));

		try
		{
			var localChain = chain;
			if (localChain == null)
				return _origStack.Peek()(weapon, result, eu);
			return localChain(weapon, result, eu);
		}
		finally
		{
			_origStack.Pop();
		}
	}
	public static bool orig_HitSomething<O, W>(O orig_, W weapon, SharedPhysics.CollisionResult result, bool eu)
		where O : Delegate
		where W : Weapon
	{
		return Invoke_HitSomething<O, W>(orig_)(weapon, result, eu);
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
	#endregion

}