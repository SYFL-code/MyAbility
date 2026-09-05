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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using UnityEngine;
using Watcher;
using static PhysicalObject;

namespace MySlugcat.Ability
{
	// 穿透能力
	internal static class Penetration
	{
		public static bool PenetrateHit<O, W>(O orig_, W weapon, SharedPhysics.CollisionResult result, bool eu)
			where O: Delegate
			where W: Weapon
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

			weapon.GetModule(out var weaponModule);
			if (weapon.thrownBy is Creature)
			{
				weaponModule.Owner = new(weapon.thrownBy);
			}
			if (weaponModule.Owner.TryGetTarget(out var owner) && owner is Player player && player.GetModule().PenetrationAbility)
			{
				Room room = weapon.room;
				if (result.obj is Creature creature && room != null)
				{
					weaponModule.stuckInObject.TryGetTarget(out var stuckInObject);
					if (stuckInObject != creature || weaponModule.stuckInObjectTime > 3)
					{
						weaponModule.stuckInObject = new(creature);
						weaponModule.stuckInObjectTime = 1;
						weaponModule.penetrateCount += 1;

						if (result.obj is Lizard lizard)
						{
							Vector2 attackDir = weapon.firstChunk.vel.normalized; // 攻击方向
							if (lizard.HitHeadShield(attackDir))
							{
								weaponModule.penetrateCount += 2;
								weapon.firstChunk.vel *= 0.8f;
							}
							else if (lizard.HitInMouth(attackDir))
							{
								creature.Violence(weapon.firstChunk, new Vector2?(weapon.firstChunk.vel * weapon.firstChunk.mass * 2f), result.chunk, result.onAppendagePos, Creature.DamageType.Stab, 0.25f, 60f);
							}
						}

						// 第1次: 100% , 第2次: 82%, 第3次: 64% ... 直到最低 15%
						float successRate = 1f - (0.18f * (weaponModule.penetrateCount - 1));
						successRate = Mathf.Clamp(successRate, 0.15f, 1f);

						float roll = UnityEngine.Random.value;

						Log.LogInfo($"穿透次数: {weaponModule.penetrateCount}, 成功率: {successRate:P2}, 随机值: {roll:F2}");

						if (roll > successRate)
						{
							return orig_HitSomething(orig_, weapon, result, eu);
						}

						if (weapon is Spear spear)
						{
							spear.stuckInObject = creature;

							float spearDamageBonus = 1f;
							switch (player.slugcatStats.throwingSkill)
							{
								case 0:
									spearDamageBonus = 0.6f + (0.3f * Mathf.Pow(UnityEngine.Random.value, 4f));
									break;

								case 1:
									spearDamageBonus = 1f;
									break;

								case 2:
									spearDamageBonus = 1.25f;
									break;

								case 3:
									spearDamageBonus = 1.5f;
									break;

								default:
									spearDamageBonus = 1f;
									break;
							}

							float MaxspearDamageBonus = Mathf.Max(spear.spearDamageBonus, spearDamageBonus);
							MaxspearDamageBonus *= Mathf.Max(0.6f, 1.1f - (0.1f * weaponModule.penetrateCount));

							if (spear.bugSpear)
							{
								MaxspearDamageBonus *= 3f;
							}

							creature.Violence(weapon.firstChunk, new Vector2?(weapon.firstChunk.vel * weapon.firstChunk.mass * 2f), result.chunk, result.onAppendagePos, Creature.DamageType.Stab, MaxspearDamageBonus, 60f);

							if (ModManager.MSC && result.obj is Player player2)
							{
								player2.playerState.permanentDamageTracking += MaxspearDamageBonus / player2.Template.baseDamageResistance;
								if (player2.playerState.permanentDamageTracking >= 1.0)
								{
									player2.Die();
								}
							}
							room.PlaySound(SoundID.Spear_Stick_In_Creature, weapon.firstChunk);
						}
						else if (weapon is Rock)
						{
							weapon.vibrate = 20;

							float stunBonus = 45f;
							if (ModManager.MMF && MMF.cfgIncreaseStuns.Value && (result.obj is Cicada || result.obj is LanternMouse || (ModManager.MSC && result.obj is Yeek)))
							{
								stunBonus = 90f;
							}
							if (ModManager.MSC && room.game.IsArenaSession && room.game.GetArenaGameSession.chMeta != null)
							{
								stunBonus = 90f;
							}

							stunBonus *= Mathf.Max(0.5f, 1.1f - (0.15f * weaponModule.penetrateCount));
							creature.Violence(weapon.firstChunk, new Vector2?(weapon.firstChunk.vel * weapon.firstChunk.mass), result.chunk, result.onAppendagePos, Creature.DamageType.Stab, 0.01f, stunBonus);

							room.PlaySound(SoundID.Rock_Hit_Creature, weapon.firstChunk);
						}
						else if (weapon is ScavengerBomb)
						{
							weapon.vibrate = 20;

							float damageBonus = 0.8f;
							float stunBonus = 85f;

							damageBonus *= Mathf.Max(0.4f, 1.1f - (0.2f * weaponModule.penetrateCount));
							stunBonus *= Mathf.Max(0.5f, 1.1f - (0.2f * weaponModule.penetrateCount));
							creature.Violence(weapon.firstChunk, new Vector2?(weapon.firstChunk.vel * weapon.firstChunk.mass), result.chunk, result.onAppendagePos, Creature.DamageType.Explosion, damageBonus, stunBonus);

							room.PlaySound(SoundID.Rock_Hit_Creature, weapon.firstChunk);
						}
						else if (ModManager.Watcher && weapon is Boomerang)
						{
							weapon.vibrate = 20;

							float damageBonus = 0.15f;
							float stunBonus = 45f;
							if (ModManager.MMF && MMF.cfgIncreaseStuns.Value && (result.obj is Cicada || result.obj is LanternMouse || (ModManager.MSC && result.obj is Yeek)))
							{
								stunBonus = 90f;
							}
							if (ModManager.MSC && room.game.IsArenaSession && room.game.GetArenaGameSession.chMeta != null)
							{
								stunBonus = 90f;
							}

							damageBonus *= Mathf.Max(0.4f, 1.1f - (0.2f * weaponModule.penetrateCount));
							stunBonus *= Mathf.Max(0.4f, 1.1f - (0.2f * weaponModule.penetrateCount));
							creature.Violence(weapon.firstChunk, new Vector2?(weapon.firstChunk.vel * weapon.firstChunk.mass), result.chunk, result.onAppendagePos, Creature.DamageType.Stab, damageBonus, stunBonus);

							room.PlaySound(WatcherEnums.WatcherSoundID.Boomerang_Collide_Creature, weapon.firstChunk);
						}
						else
						{
							float damageBonus = 0.15f;
							float stunBonus = 20f;

							damageBonus *= Mathf.Max(0.1f, 1.1f - (0.3f * weaponModule.penetrateCount));
							stunBonus *= Mathf.Max(0.05f, 1.1f - (0.3f * weaponModule.penetrateCount));
							creature.Violence(weapon.firstChunk, new Vector2?(weapon.firstChunk.vel * weapon.firstChunk.mass), result.chunk, result.onAppendagePos, Creature.DamageType.Stab, damageBonus, stunBonus);

							room.PlaySound(SoundID.Rock_Hit_Creature, weapon.firstChunk);
						}
						weapon.firstChunk.vel *= 0.8f;

						//震动强度
						weapon.vibrate = 20;

						// 屏幕震动
						//room.ScreenMovement(null, dir, strength);
					}
					else
					{
						weaponModule.stuckInObjectTime += 1;
					}
				}
				//result.obj = null;
				return false;
			}
			else
			{
				return orig_HitSomething(orig_, weapon, result, eu);
			}
		}
		public static bool orig_HitSomething<O, W>(O orig_, W weapon, SharedPhysics.CollisionResult result, bool eu)
			where O : Delegate
			where W : Weapon
		{
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

		public static void Weapon_HitAnotherThrownWeapon(On.Weapon.orig_HitAnotherThrownWeapon orig, Weapon weapon, Weapon obj)
		{
			if ((obj.firstChunk.pos.x < obj.firstChunk.lastPos.x) == (weapon.firstChunk.pos.x < weapon.firstChunk.lastPos.x))
			{
				orig(weapon, obj);
				return;
			}

			weapon.GetModule(out var weaponModule);
			obj.GetModule(out var weaponModule2);

			bool flag = weaponModule.Owner.TryGetTarget(out var target) && target is Player player && player.GetModule().PenetrationAbility;
			bool flag2 = weaponModule2.Owner.TryGetTarget(out var target2) && target2 is Player player2 && player2.GetModule().PenetrationAbility;

			

			if ((!flag && !flag2) || (flag && flag2))
			{
				orig(weapon, obj);
				return;
			}

			if (flag || flag2)
			{
				Room room = weapon.room;
				if (room != null)
				{
					Vector2 vector = Vector2.Lerp(obj.firstChunk.lastPos, weapon.firstChunk.lastPos, 0.5f);

					int SparkQuantity = 3;
					if (weapon is Spear)
					{
						SparkQuantity += 2;
					}
					if (obj is Spear)
					{
						SparkQuantity += 2;
					}
					for (int i = 0; i < SparkQuantity; i++)
					{
						room.AddObject(new Spark(vector + (Custom.DegToVec(UnityEngine.Random.value * 360f) * 5f * UnityEngine.Random.value),
							Custom.DegToVec(UnityEngine.Random.value * 360f) * Mathf.Lerp(2f, 7f, UnityEngine.Random.value) * SparkQuantity,
							new Color(1f, 1f, 1f), null, 10, 170));
					}

					Vector2 vector2 = Custom.DegToVec(UnityEngine.Random.value * 360f);
					if (flag2)
					{
						weapon.WeaponDeflect(vector, vector2, weapon.firstChunk.vel.magnitude);
					}
					if (flag)
					{
						obj.WeaponDeflect(vector, -vector2, weapon.firstChunk.vel.magnitude);
					}

					room.PlaySound(SoundID.Spear_Bounce_Off_Creauture_Shell, vector, weapon.abstractPhysicalObject);
				}
			}
		}


	}
}
