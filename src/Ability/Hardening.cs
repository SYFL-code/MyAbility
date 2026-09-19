using CommonUtils.Core;
using IL;
using ImprovedInput;
using Menu.Remix;
using Mono.Cecil;
using MonoMod.RuntimeDetour;
using MoreSlugcats;
using On;
using RewiredConsts;
using RWCustom;
using SlugBase.Features;
using Smoke;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using UnityEngine;
using Watcher;
using static MySlugcat.Ability.Camouflage;
using static PhysicalObject;

namespace MySlugcat.Ability
{
	// 硬化
	public static class Hardening
	{
		public static bool Hardening_HitSomething<O, W>(O orig_, W weapon, SharedPhysics.CollisionResult result, bool eu)
			where O : Delegate
			where W : Weapon
		{
			if (result.obj == null)
			{
				return orig_HitSomething(orig_, weapon, result, eu);
			}
			if (result.obj.abstractPhysicalObject.rippleLayer != weapon.abstractPhysicalObject.rippleLayer &&
				!result.obj.abstractPhysicalObject.rippleBothSides && !weapon.abstractPhysicalObject.rippleBothSides)
			{
				return orig_HitSomething(orig_, weapon, result, eu);
			}


			if (result.obj is Player player)
			{
				if (player.GetModule().HardeningAbility && player.GetHardeningModule().EnableHardening)
				{
					float weaponSpeed = weapon.firstChunk.vel.magnitude;// 一般为40f

					float chance = 0.01f;
					chance += weaponSpeed > 40f ? weaponSpeed  /1000f * 2 : 0f;
					chance += player.bodyMode == Player.BodyModeIndex.ClimbingOnBeam ? 0.01f : 0f;

					chance = Mathf.Clamp01(chance);
					if (UnityEngine.Random.value < chance)
					{
						player.Stun(40);
					}
					if (player.bodyMode == Player.BodyModeIndex.ClimbingOnBeam && UnityEngine.Random.value < 0.03f)
					{
						player.Stun(1);
					}

					if (weaponSpeed < 60f || UnityEngine.Random.value < 0.10f)
					{
						result.obj = null;
						result.chunk = null;
						result.onAppendagePos = null;

						HitAnotherPhysicalObject(player, weapon, false);
					}

				}
			}
			return orig_HitSomething(orig_, weapon, result, eu);
		}
		public static bool orig_HitSomething<O, W>(O orig_, W weapon, SharedPhysics.CollisionResult result, bool eu)
			where O : Delegate
			where W : Weapon
		{
			return Hooks.orig_HitSomething(orig_, weapon, result, eu);
		}


		public static HardeningModule GetHardeningModule(this Player player, out HardeningModule module)
		{
			module = GetHardeningModule(player);
			return module;
		}
		public static HardeningModule GetHardeningModule(this Player player)
		{
			return ModuleManager.Get(player, p => new HardeningModule());
		}
		public class HardeningModule
		{
			public bool EnableHardening = false;
			public int HardeningCounter;
			public int HardeningCdCounter;
		}

		public static void Player_Update(On.Player.orig_Update orig, Player player, bool eu)
		{
			player.GetModule(out var playerModule);
			if (playerModule.HardeningAbility)
			{
				player.GetHardeningModule(out var module);

				if (module.HardeningCounter > 0)
				{
					module.HardeningCounter--;
				}
				if (module.HardeningCdCounter > 0)
				{
					module.HardeningCdCounter--;
				}
				if (module.HardeningCounter <= 0)
				{
					module.EnableHardening = false;
				}

				if (Input.GetKey("x"))
				{
					if (module.HardeningCdCounter <= 0)
					{
						module.HardeningCounter = 10 * 40;
						module.HardeningCdCounter = 30 * 40;
						module.EnableHardening = true;

						//player.room.AddObject(new CommonUtils.Core.DebugSprite(player, 10 * 40));
					}
				}
			}

			orig(player, eu);
		}

		public static void HitAnotherPhysicalObject(PhysicalObject physicalObject, PhysicalObject obj, bool check)
		{
			{
				if (physicalObject is Weapon weapon && obj is Weapon weaponObj)
				{
					weapon.HitAnotherThrownWeapon(weaponObj);
				}
			}

			if (check)
			{
				if ((obj.firstChunk.pos.x < obj.firstChunk.lastPos.x) == (physicalObject.firstChunk.pos.x < physicalObject.firstChunk.lastPos.x))
				{
					return;
				}
			}

			{
				Creature? creature = (physicalObject as Creature) ?? (obj as Creature);
				Weapon? weapon = (physicalObject as Weapon) ?? (obj as Weapon);

				if (creature != null && weapon != null)
				{
					Vector2 push = weapon.firstChunk.vel * weapon.firstChunk.mass / creature.firstChunk.mass;
					creature.firstChunk.vel += push;
				}
			}

			Vector2 vector = Vector2.Lerp(obj.firstChunk.lastPos, physicalObject.firstChunk.lastPos, 0.5f);

			int num = 3;
			if (physicalObject is Spear)
			{
				num += 2;
			}
			if (obj is Spear)
			{
				num += 2;
			}
			for (int i = 0; i < num; i++)
			{
				physicalObject.room.AddObject(new Spark(
					vector + (Custom.DegToVec(UnityEngine.Random.value * 360f) * (5f * UnityEngine.Random.value)),
					Custom.DegToVec(UnityEngine.Random.value * 360f) * (Mathf.Lerp(2f, 7f, UnityEngine.Random.value) * num),
					new Color(1f, 1f, 1f), null, 10, 170));
			}

			{
				Vector2 vector2 = Custom.DegToVec(UnityEngine.Random.value * 360f);
				if (physicalObject is Weapon weapon)
				{
					weapon.WeaponDeflect(vector, vector2, physicalObject.firstChunk.vel.magnitude);
				}
				if (obj is Weapon weaponObj)
				{
					weaponObj.WeaponDeflect(vector, -vector2, obj.firstChunk.vel.magnitude);
				}
			}

			physicalObject.room.PlaySound(SoundID.Spear_Bounce_Off_Creauture_Shell, vector, physicalObject.abstractPhysicalObject);
		}


	}
}
