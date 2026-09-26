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
using static Menu.Remix.InternalOI;
using static PhysicalObject;

namespace MySlugcat
{
	public static class ModuleExtensions
	{
		#region Player
		public static PlayerModule GetModule(this Player player, out PlayerModule module)
		{
			module = player.Module;
			return module;
		}
		extension(Player player)
		{
			public PlayerModule Module => (PlayerModule)((PhysicalObject)player).GetModule();
		}
		#endregion

		#region Creature
		public static CreatureModule GetModule(this Creature creature)
		{
			return creature.Module;
		}
		public static CreatureModule GetModule(this Creature creature, out CreatureModule module)
		{
			module = creature.Module;
			return module;
		}
		extension(Creature creature)
		{
			public CreatureModule Module => (CreatureModule)((PhysicalObject)creature).GetModule();
		}
		#endregion

		#region PhysicalObject
		public static PhysicalObjectModule GetModule(this PhysicalObject physicalObject)
		{
			return ModuleManager.Get<PhysicalObject, PhysicalObjectModule>(physicalObject, obj =>
			{
				if (obj is Player p)
					return new PlayerModule(p);

				else if(obj is Creature c)
					return new CreatureModule(c);

				else if (obj is Weapon w)
					return new WeaponModule(w);

				else
					return new PhysicalObjectModule(obj);
			});

			//return ModuleManager.Get(physicalObject, p => new PhysicalObjectModule(p));
		}
		public static PhysicalObjectModule GetModule(this PhysicalObject physicalObject, out PhysicalObjectModule module)
		{
			module = physicalObject.Module;
			return module;
		}
		extension(PhysicalObject physicalObject)
		{
			public PhysicalObjectModule Module => physicalObject.GetModule();
		}
		#endregion

		#region Weapon
		public static WeaponModule GetModule(this Weapon weapon, out WeaponModule module)
		{
			module = weapon.Module;
			return module;
		}
		extension(Weapon weapon)
		{
			public WeaponModule Module => (WeaponModule)((PhysicalObject)weapon).GetModule();
		}
		#endregion
	}

	public class PlayerModule : CreatureModule
	{
		private WeakReference<Player> _playerRef;

		//public bool PenetrationAbility = false;
		//public bool FrameAbility = false;
		//public bool ArcLightningAbility = false;
		public bool CamouflageAbility = false;
		//public bool DeflagrationAbility = false;
		//public bool TrackingThrowAbility = false;
		public bool ExtraGraspAbility = false;

		public PlayerModule(Player player):
			base(player)
		{
			_playerRef = new WeakReference<Player>(player);

			if (player.slugcatStats.name == SlugcatStats.Name.White)
			{
				//PenetrationAbility = true;
				//FrameAbility = true;
				//ArcLightningAbility = true;
				CamouflageAbility = true;
				//DeflagrationAbility = true;
				//TrackingThrowAbility = true;
			}
			if (Debugger.bools[2, true, "ExtraGraspAbility"])
			{
				ExtraGraspAbility = true;
			}
		}
	}

	public class CreatureModule : PhysicalObjectModule
	{
		private WeakReference<Creature> _creatureRef;

		public bool PenetrationAbility = false;
		public bool FrameAbility = false;
		public bool ArcLightningAbility = false;
		public bool DeflagrationAbility = false;
		public bool StalwartShellAbility = false;
		public bool TrackingThrowAbility = false;

		public CreatureModule(Creature creature) :
			base(creature)
		{
			_creatureRef = new WeakReference<Creature>(creature);

			if (Plugin.DebugMode)
			{
				if (creature is Player player)
				{
					if (player.slugcatStats.name == SlugcatStats.Name.White)
					{
						//PenetrationAbility = true;
						FrameAbility = true;
						//ArcLightningAbility = true;
						DeflagrationAbility = true;
						TrackingThrowAbility = true;
					}
				}
				else
				{
				}
				StalwartShellAbility = true;
			}
		}
	}

	public class PhysicalObjectModule
	{
		public PhysicalObjectModule(PhysicalObject physicalObject)
		{

		}
	}

	public class WeaponModule : PhysicalObjectModule
	{
		public WeakReference<PhysicalObject?> Owner = new(null);

		// 武器穿透对象
		public WeakReference<PhysicalObject?> stuckInObject = new(null);
		// 武器穿透时长
		public int stuckInObjectTime = 0;
		// 武器穿透次数
		public int penetrateCount = 0;

		public WeaponModule(Weapon weapon) :
			base(weapon)
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
			else if (weaponModule.Owner.TryGetTarget(out var owner) && owner is Creature thrownBy)
			{
				weapon.thrownBy = thrownBy;
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
