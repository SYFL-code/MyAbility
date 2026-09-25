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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using Watcher;
using static CommonUtils.Core.HookManager;
using static PhysicalObject;

namespace MySlugcat
{
	public static class ModuleExtensions
	{
		public static PlayerModule GetModule(this Player player)
		{
			return ModuleManager.Get<Player, PlayerModule>(player, p => new PlayerModule(p));
		}
		public static PlayerModule GetModule(this Player player, out PlayerModule module)
		{
			module = GetModule(player);
			return module;
		}

		public static WeaponModule GetModule(this Weapon weapon)
		{
			return ModuleManager.Get(weapon, w => new WeaponModule(w));
		}
		public static WeaponModule GetModule(this Weapon weapon, out WeaponModule module)
		{
			module = GetModule(weapon);
			return module;
		}
	}

	public class PlayerModule
	{
		private WeakReference<Player> _playerRef;

		public bool PenetrationAbility = false;
		public bool FrameAbility = false;
		public bool ArcLightningAbility = false;
		public bool CamouflageAbility = false;
		public bool DeflagrationAbility = false;
		public bool TrackingThrowAbility = false;
		public bool ExtraGraspAbility = false;

		public PlayerModule(Player player)
		{
			_playerRef = new WeakReference<Player>(player);

			if (player.slugcatStats.name == SlugcatStats.Name.White)
			{
				PenetrationAbility = true;
				FrameAbility = true;
				ArcLightningAbility = true;
				CamouflageAbility = true;
				DeflagrationAbility = true;
				TrackingThrowAbility = true;
			}
			if (Debugger.bools[2, true, "ExtraGraspAbility"])
			{
				ExtraGraspAbility = true;
			}
		}
	}

	public class WeaponModule
	{
		public WeakReference<PhysicalObject?> Owner = new(null);

		// 武器穿透对象
		public WeakReference<PhysicalObject?> stuckInObject = new(null);
		// 武器穿透时长
		public int stuckInObjectTime = 0;
		// 武器穿透次数
		public int penetrateCount = 0;

		public WeaponModule(Weapon weapon)
		{

		}
	}

	public static class ModuleHooks
	{
		public static bool Weapon_HitSomething<O, W>(O orig_, W weapon, SharedPhysics.CollisionResult result, bool eu)
			where O : Delegate
			where W : Weapon
		{
			weapon.GetModule(out var weaponModule);
			if (weapon.thrownBy != null)
			{
				weaponModule.Owner = new(weapon.thrownBy);
			}

			return Hooks.orig_HitSomething(orig_, weapon, result, eu);
		}

		public static void Weapon_Thrown(On.Weapon.orig_Thrown orig, Weapon weapon, Creature thrownBy, Vector2 thrownPos,
			Vector2? firstFrameTraceFromPos, IntVector2 throwDir, float frc, bool eu)
		{
			orig(weapon, thrownBy, thrownPos, firstFrameTraceFromPos, throwDir, frc, eu);

			weapon.GetModule(out var weaponModule);
			weaponModule.Owner = new(thrownBy);
		}

		public static void Weapon_Update(On.Weapon.orig_Update orig, Weapon weapon, bool eu)
		{
			orig(weapon, eu);

			weapon.GetModule(out var weaponModule);
			if (weapon.mode != Weapon.Mode.Thrown && weapon.mode != Weapon.Mode.StuckInCreature)
			{
				weaponModule.stuckInObject = new(null);
				weaponModule.stuckInObjectTime = 0;
				weaponModule.penetrateCount = 0;
			}

			//if (weapon.mode != Weapon.Mode.Thrown && weapon.mode != Weapon.Mode.StuckInCreature &&
			//	weapon.thrownBy != null && weapon.thrownBy is Player player2 && player2.GetModule().PenetrationAbility)
			//{
			//	weapon.thrownBy = null;
			//}

			/*if ((weapon.mode != Weapon.Mode.Thrown && weapon.mode != Weapon.Mode.StuckInCreature) && 
				weapon.firstChunk.owner != null && weapon.firstChunk.owner is Player player__ && player__.GetModule().PenetrationSkill)
			{
				weapon.firstChunk.owner = null;
			}*/
		}

	}
}
