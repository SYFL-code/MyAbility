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
	// 坚硬外壳
	public static class StalwartShell
	{
		public static bool StalwartShell_HitSomething<O, W>(O orig_, W weapon, SharedPhysics.CollisionResult result, bool eu)
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


			if (result.obj is Creature self)
			{
				if (self.GetModule().StalwartShellAbility && self.GetStalwartShellModule(out var module).validity)
				{
					weapon.GetModule(out var weaponModule);
					if (weaponModule.Owner.TryGetTarget(out var owner) && owner is Creature thrownBy &&
						!thrownBy.GetModule().PenetrationAbility)
					{
						Player? player = self as Player;

						//float weaponSpeed = weapon.firstChunk.vel.magnitude;// 一般为40f

						//float chance = 0.01f;
						//chance += weaponSpeed > 40f ? weaponSpeed  /1000f * 2 : 0f;
						//chance += player?.bodyMode == Player.BodyModeIndex.ClimbingOnBeam ? 0.02f : 0f;

						//chance = Mathf.Clamp01(chance);
						//if (UnityEngine.Random.value < chance)
						//{
						//	self.Stun(40);
						//}
						//if (player?.bodyMode == Player.BodyModeIndex.ClimbingOnBeam && UnityEngine.Random.value < 0.03f)
						//{
						//	self.Stun(1);
						//}

						//if (weaponSpeed < 60f || UnityEngine.Random.value < 0.10f)
						//{
						//	result.obj = null;
						//	result.chunk = null;
						//	result.onAppendagePos = null;

						//	HitAnotherPhysicalObject(player, weapon, false);

						//}

						self.Violence(weapon.firstChunk, weapon.firstChunk.vel * weapon.firstChunk.mass * 2f,
							result.chunk, result.onAppendagePos,
							Creature.DamageType.None, 0f, 0f);

						result.obj = null;
						result.chunk = null;
						result.onAppendagePos = null;

						HitAnotherPhysicalObject(self, weapon, false);

						module.lastHitOffset = weapon.firstChunk.pos - self.mainBodyChunk.pos;
						HitEffect(self, weapon.firstChunk.pos + weapon.firstChunk.vel, weapon.firstChunk.vel);
						AddDamage(self, weapon.HeavyWeapon ? 0.5f : 0.2f);
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

		public static void Creature_Update(On.Creature.orig_Update orig, Creature self, bool eu)
		{
			if (self.GetModule().StalwartShellAbility)
			{
				self.GetStalwartShellModule(out var module);

				bool lastValidity = module.validity;
				if (module.validity)
				{
					if (!module.initRestistances)
					{
						module.initRestistances = true;

						// 复制一份个体专用模板
						var newTemplate = new CreatureTemplate(self.Template);

						// 修改副本的抗性
						SetResistance(newTemplate, Creature.DamageType.Blunt, 0.85f, 0.85f);
						SetResistance(newTemplate, Creature.DamageType.Stab, 0.55f, 0.55f);
						SetResistance(newTemplate, Creature.DamageType.Bite, 0.5f, 0.5f);
						SetResistance(newTemplate, Creature.DamageType.Explosion, 0.7f, 0.7f);

						// 替换个体模板
						module.origTemplate = self.abstractCreature.creatureTemplate;
						self.abstractCreature.creatureTemplate = newTemplate;
					}
				}

				if (module.damage >= 1 && module.validity && UnityEngine.Random.value < 0.015f)
				{
					Shatter(self, module.lastHitOffset + self.mainBodyChunk.pos);
				}

				if (lastValidity && !module.validity)
				{
					if (module.origTemplate != null)
					{
						self.abstractCreature.creatureTemplate = module.origTemplate;
					}
				}
			}

			//player.GetModule(out var playerModule);
			//if (playerModule.HardeningAbility)
			//{
			//	player.GetHardeningModule(out var module);

			//	if (module.HardeningCounter > 0)
			//	{
			//		module.HardeningCounter--;
			//	}
			//	if (module.HardeningCdCounter > 0)
			//	{
			//		module.HardeningCdCounter--;
			//	}
			//	if (module.HardeningCounter <= 0)
			//	{
			//		module.EnableHardening = false;
			//	}

			//	if (Input.GetKey("x"))
			//	{
			//		if (module.HardeningCdCounter <= 0)
			//		{
			//			module.HardeningCounter = 10 * 40;
			//			module.HardeningCdCounter = 30 * 40;
			//			module.EnableHardening = true;

			//			//player.room.AddObject(new CommonUtils.Core.DebugSprite(player, 10 * 40));
			//		}
			//	}
			//}

			orig(self, eu);
		}
		private static void SetResistance(CreatureTemplate template, Creature.DamageType type, float dmgScale, float stunScale)
		{
			if (type.Index < 0 || type.Index >= template.damageRestistances.GetLength(0)) return;
			template.damageRestistances[type.Index, 0] /= dmgScale;
			template.damageRestistances[type.Index, 1] /= stunScale;
		}

		private static float Rand => UnityEngine.Random.value;
		public static void HitEffect(Creature creature, Vector2 impactPos, Vector2 impactVelocity)
		{
			var num = UnityEngine.Random.Range(3, 8);
			for (int k = 0; k < num; k++)
			{
				Vector2 pos = impactPos + (Custom.DegToVec(Rand * 360f) * 5f * Rand);
				Vector2 vel = (-impactVelocity * -0.1f) + (Custom.DegToVec(Rand * 360f) * Mathf.Lerp(0.2f, 0.4f, Rand) * impactVelocity.magnitude);
				creature.room.AddObject(new Spark(pos, vel, new Color(1f, 1f, 1f), null, 10, 170));
			}

			creature.room.AddObject(new StationaryEffect(impactPos, new Color(1f, 1f, 1f), null, StationaryEffect.EffectType.FlashingOrb));
		}
		public static void AddDamage(Creature creature, float damage)
		{
			creature.GetStalwartShellModule(out var module);

			module.damage += damage * 0.2f;

			if (module.damage > 1)
				module.damage = 1;
		}
		public static void Shatter(Creature creature, Vector2 impactPos)
		{
			var num = UnityEngine.Random.Range(6, 10);
			for (int k = 0; k < num; k++)
			{
				Vector2 pos = impactPos + (Custom.RNV() * 5f * Rand);
				Vector2 vel = Custom.RNV() * 4f * (1 + Rand);
				creature.room.AddObject(new Spark(pos, vel, new Color(1f, 1f, 1f), null, 10, 170));
			}

			//float count = 2 + (4 * (Abstr.scaleX + Abstr.scaleY));

			//for (int j = 0; j < count; j++)
			//{
			//	Vector2 extraVel = Custom.RNV() * Rand * (j == 0 ? 3f : 6f);

			//	creature.room.AddObject(new CentipedeShell(firstChunk.pos, Custom.RNV() * Rand * 15 + extraVel, Abstr.hue, Abstr.saturation, 0.25f, 0.25f));
			//}

			creature.room.PlaySound(SoundID.Weapon_Skid, impactPos, 0.75f, 1.25f);

			creature.GetStalwartShellModule(out var module);
			module.validity = false;
			//AllGraspsLetGoOfThisObject(true);
			//abstractPhysicalObject.LoseAllStuckObjects();
			//Destroy();
		}

		public static void HitAnotherPhysicalObject(PhysicalObject physicalObject, PhysicalObject obj, bool validity)
		{
			Weapon? weapon = physicalObject as Weapon;
			Weapon? weaponObj = obj as Weapon;

			if (weapon != null && weaponObj != null)
			{
				weapon.HitAnotherThrownWeapon(weaponObj);
			}

			if (validity)
			{
				if ((obj.firstChunk.pos.x < obj.firstChunk.lastPos.x) == (physicalObject.firstChunk.pos.x < physicalObject.firstChunk.lastPos.x))
				{
					return;
				}
			}

			Creature? c = (physicalObject as Creature) ?? (obj as Creature);
			Weapon? w = (physicalObject as Weapon) ?? (obj as Weapon);

			if (c != null && w != null)
			{
				Vector2 push = w.firstChunk.vel * w.firstChunk.mass / c.firstChunk.mass;
				c.firstChunk.vel += push;
			}


			Vector2 inbetweenPos = Vector2.Lerp(obj.firstChunk.lastPos, physicalObject.firstChunk.lastPos, 0.5f);

			int num = 3;
			if (physicalObject is Spear)
				num += 2;
			if (obj is Spear)
				num += 2;

			for (int i = 0; i < num; i++)
			{
				physicalObject.room.AddObject(new Spark(
					inbetweenPos + (Custom.DegToVec(UnityEngine.Random.value * 360f) * (5f * UnityEngine.Random.value)),
					Custom.DegToVec(UnityEngine.Random.value * 360f) * (Mathf.Lerp(2f, 7f, UnityEngine.Random.value) * num),
					new Color(1f, 1f, 1f), null, 10, 170));
			}

			Vector2 deflectDir = Custom.DegToVec(UnityEngine.Random.value * 360f);
			weapon?.WeaponDeflect(inbetweenPos, deflectDir, physicalObject.firstChunk.vel.magnitude);
			weaponObj?.WeaponDeflect(inbetweenPos, -deflectDir, obj.firstChunk.vel.magnitude);

			physicalObject.room.PlaySound(SoundID.Spear_Bounce_Off_Creauture_Shell, inbetweenPos, physicalObject.abstractPhysicalObject);
		}

		public class StalwartShellModule
		{
			public bool validity = true;
			public float damage;
			public Vector2 lastHitOffset;

			public bool initRestistances;
			public CreatureTemplate? origTemplate;
			//public float?[,] damageRestistances;

			//public bool EnableHardening = false;
			//public int HardeningCounter;
			//public int HardeningCdCounter;

			public StalwartShellModule(Creature self)
			{
				//damageRestistances = new float?[ExtEnum<Creature.DamageType>.values.Count, 2];

				if (Plugin.DebugMode)
				{
					if (self is Player)
					{
						damage = -9999;
					}
				}
			}
		}
		public static StalwartShellModule GetStalwartShellModule(this Creature self, out StalwartShellModule module)
		{
			module = GetStalwartShellModule(self);
			return module;
		}
		public static StalwartShellModule GetStalwartShellModule(this Creature self)
		{
			return ModuleManager.Get(self, s => new StalwartShellModule(s));
		}

	}
}
