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
	internal static class PenetrationAbility
	{
		public static void Hook()
		{
            // Update
            HookManager.Register("On.Weapon.Update += Weapon_Update (Penetration)", new HookManager.HookData
			{
				Priority = 0,
				InitializeHooks = () => On.Weapon.Update += Weapon_Update,
				UnInitializeHooks = () => On.Weapon.Update -= Weapon_Update,
			});

            // Thrown
            HookManager.Register("On.Weapon.Thrown += Weapon_Thrown (Penetration)", new HookManager.HookData
			{
				Priority = 0,
				InitializeHooks = () => On.Weapon.Thrown += Weapon_Thrown,
				UnInitializeHooks = () => On.Weapon.Thrown -= Weapon_Thrown,
			});

            // HitAnotherThrownWeapon
            HookManager.Register("On.Weapon.HitAnotherThrownWeapon += Weapon_HitAnotherThrownWeapon (Penetration)", new HookManager.HookData
			{
				Priority = 0,
				InitializeHooks = () => On.Weapon.HitAnotherThrownWeapon += Weapon_HitAnotherThrownWeapon,
				UnInitializeHooks = () => On.Weapon.HitAnotherThrownWeapon -= Weapon_HitAnotherThrownWeapon,
			});


            // HitSomething
            HookManager.Register("On.Weapon.HitSomething += PenetrateHit (Penetration)", new HookManager.HookData
			{
				Priority = 1,
				InitializeHooks = () => On.Weapon.HitSomething += PenetrateHit,
				UnInitializeHooks = () => On.Weapon.HitSomething -= PenetrateHit,
			});
			HookManager.Register("On.Spear.HitSomething += PenetrateHit (Penetration)", new HookManager.HookData
			{
				Priority = 1,
				InitializeHooks = () => On.Spear.HitSomething += PenetrateHit,
				UnInitializeHooks = () => On.Spear.HitSomething -= PenetrateHit,
			});
			HookManager.Register("On.Rock.HitSomething += PenetrateHit (Penetration)", new HookManager.HookData
			{
				Priority = 1,
				InitializeHooks = () => On.Rock.HitSomething += PenetrateHit,
				UnInitializeHooks = () => On.Rock.HitSomething -= PenetrateHit,
			});
			HookManager.Register("On.ScavengerBomb.HitSomething += PenetrateHit (Penetration)", new HookManager.HookData
			{
				Priority = 1,
				InitializeHooks = () => On.ScavengerBomb.HitSomething += PenetrateHit,
				UnInitializeHooks = () => On.ScavengerBomb.HitSomething -= PenetrateHit,
			});
			if (ModManager.MSC)
			{
				HookManager.Register("On.MoreSlugcats.LillyPuck.HitSomething += PenetrateHit (Penetration)", new HookManager.HookData
				{
					Priority = 1,
					InitializeHooks = () => On.MoreSlugcats.LillyPuck.HitSomething += PenetrateHit,
					UnInitializeHooks = () => On.MoreSlugcats.LillyPuck.HitSomething -= PenetrateHit,
				});
			}
			if (ModManager.Watcher)
			{
				HookManager.Register("On.Boomerang.HitSomething += PenetrateHit (Penetration)", new HookManager.HookData
				{
					Priority = 1,
					InitializeHooks = () => On.Boomerang.HitSomething += PenetrateHit,
					UnInitializeHooks = () => On.Boomerang.HitSomething -= PenetrateHit,
				});
			}

		}

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

			if (weapon.thrownBy is Player player && player.GetModule().PenetrationAbility)
			{
				Room room = weapon.room;
				if (result.obj is Creature creature && room != null)
				{
					weapon.GetModule(out var weaponModule);

					weaponModule.stuckInObject.TryGetTarget(out var target);
					if (target != creature || weaponModule.stuckInObjectTime > 30)
					{
						weaponModule.stuckInObject = new(creature);
						weaponModule.stuckInObjectTime = 1;
						weaponModule.penetrateCount += 1;

						if (UnityEngine.Random.value * 10f > Mathf.Max(6.6f, 12f - weaponModule.penetrateCount))
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
							creature.Violence(weapon.firstChunk, new Vector2?(weapon.firstChunk.vel * weapon.firstChunk.mass), result.chunk, result.onAppendagePos, Creature.DamageType.Stab, 0.12f, stunBonus);

							room.PlaySound(SoundID.Rock_Hit_Creature, weapon.firstChunk);
						}
						else if (weapon is ScavengerBomb)
						{
							weapon.vibrate = 20;

							creature.Violence(weapon.firstChunk, new Vector2?(weapon.firstChunk.vel * weapon.firstChunk.mass), result.chunk, result.onAppendagePos, Creature.DamageType.Explosion, 0.8f, 85f);

							room.PlaySound(SoundID.Rock_Hit_Creature, weapon.firstChunk);
						}
						else if (ModManager.Watcher && weapon is Boomerang)
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

							creature.Violence(weapon.firstChunk, new Vector2?(weapon.firstChunk.vel * weapon.firstChunk.mass), result.chunk, result.onAppendagePos, Creature.DamageType.Stab, 0.15f, stunBonus);

							room.PlaySound(WatcherEnums.WatcherSoundID.Boomerang_Collide_Creature, weapon.firstChunk);
						}
						else
						{
							creature.Violence(weapon.firstChunk, new Vector2?(weapon.firstChunk.vel * weapon.firstChunk.mass), result.chunk, result.onAppendagePos, Creature.DamageType.Stab, 0.2f, 20);

							room.PlaySound(SoundID.Rock_Hit_Creature, weapon.firstChunk);
						}

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
