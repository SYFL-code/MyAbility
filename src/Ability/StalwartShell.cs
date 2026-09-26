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
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using UnityEngine;
using Watcher;
using static MySlugcat.Ability.Camouflage;
using static PhysicalObject;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using static UnityEngine.UI.Image;

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



			if (result.obj is Creature creature)
			{
				if (creature.Module.StalwartShellAbility && creature.Shell.validity)
				{
					if (weapon.Module.Owner.TryGetTarget(out var owner) && owner is Creature thrownBy &&
						thrownBy.Module.PenetrationAbility)
					{
						return orig_HitSomething(orig_, weapon, result, eu);
					}

					Player? player = creature as Player;

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

					creature.Violence(weapon.firstChunk, weapon.firstChunk.vel * weapon.firstChunk.mass * 2f,
						result.chunk, result.onAppendagePos,
						Creature.DamageType.None, 0f, 0f);

					//result.obj = null;
					//result.chunk = null;
					//result.onAppendagePos = null;

					HitAnotherPhysicalObject(creature, weapon, false);

					creature.Shell.lastHitOffset = weapon.firstChunk.pos - creature.mainBodyChunk.pos;
					HitEffect(creature, weapon.firstChunk.pos + (weapon.firstChunk.vel * 2f), weapon.firstChunk.vel);
					AddDamage(creature, weapon.HeavyWeapon ? 0.5f : 0.2f);

					return false;
					//return orig_HitSomething(orig_, weapon, result, eu);
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

		public static void Creature_Update(On.Creature.orig_Update orig, Creature creature, bool eu)
		{
			if (creature.Module.StalwartShellAbility)
			{
				var Shell = creature.Shell;
				
				bool lastValidity = Shell.validity;
				if (Shell.validity)
				{
					//if (!Shell.initRestistances)
					//{
					//	Shell.initRestistances = true;

					//	// 复制一份个体专用模板
					//	var newTemplate = new CreatureTemplate(self.Template);

					//	// 修改副本的抗性
					//	SetResistance(newTemplate, Creature.DamageType.Blunt, 0.85f, 0.85f);
					//	SetResistance(newTemplate, Creature.DamageType.Stab, 0.55f, 0.55f);
					//	SetResistance(newTemplate, Creature.DamageType.Bite, 0.5f, 0.5f);
					//	SetResistance(newTemplate, Creature.DamageType.Explosion, 0.7f, 0.7f);

					//	// 替换个体模板
					//	module.origTemplate = self.abstractCreature.creatureTemplate;
					//	self.abstractCreature.creatureTemplate = newTemplate;
					//}
				}

				if (Shell.damage >= 1 && Shell.validity && UnityEngine.Random.value < 0.015f)
				{
					Shatter(creature, Shell.lastHitOffset + creature.mainBodyChunk.pos);
				}

				//if (lastValidity && !Shell.validity)
				//{
				//	if (Shell.origTemplate != null)
				//	{
				//		creature.abstractCreature.creatureTemplate = Shell.origTemplate;
				//	}
				//}

				if (Plugin.DebugMode)
				{
					if (creature is Player)
					{
						if (Input.GetKey("x"))
						{
							Shell.damage = 0;
							Shell.validity = true;
						}
					}
				}
			}

			orig(creature, eu);
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
			var Shell = creature.Shell;

			Shell.damage += damage * 0.2f;
			if (Shell.damage > 1)
				Shell.damage = 1;
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

			creature.Shell.validity = false;
			//AllGraspsLetGoOfThisObject(true);
			//abstractPhysicalObject.LoseAllStuckObjects();
			//Destroy();
		}


		public static void InitiateSprites(On.GraphicsModule.orig_InitiateSprites orig, GraphicsModule graphicsModule,
			RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
		{
			orig(graphicsModule, sLeaser, rCam);


			if (graphicsModule.owner is Creature creature)
			{
				if (creature.Module.StalwartShellAbility)
				{
					Shell Shell = creature.Shell;

					foreach (var sprite in Shell.sprites)
					{
						if (sprite != null)
						{
							sprite.RemoveFromContainer();
						}
					}
					Shell.sprites = new FSprite?[2, sLeaser.sprites.Length];
					for (int j = 0; j < Shell.sprites.GetLength(0); j++)
					{
						for (int i = 0; i < Shell.sprites.GetLength(1); i++)
						{
							if (creature is not Player || !sLeaser.sprites[i].element.name.StartsWith("Leg"))
							{
								FSprite copy = CloneFSprite(sLeaser.sprites[i]);
								//FSprite copy = new FSprite("Circle20");

								Shell.sprites[j, i] = copy;
								//Shell.sprites[i]!.scaleX = graphicsModule.owner.bodyChunks[i].rad / 16f;
								//Shell.sprites[i]!.scaleY = graphicsModule.owner.bodyChunks[i].rad / 16f;

								//Shell.sprites[i]!.color = new Color(220f / 256f, 220 / 256f, 170 / 256f);
								if (j == 0)
								{
									Shell.sprites[j, i]!.color = new Color(0.01f, 0f, 0f);
								}
								if (j == 1)
								{
									Shell.sprites[j, i]!.color = creature.ShortCutColor().HSV(1f, 1.5f, 0.5f);
								}

								rCam.ReturnFContainer("Background").AddChild(Shell.sprites[j, i]);
							}
							else
							{
								Shell.sprites[j, i] = null;
							}
						}
					}
				}
			}
		}
		public static void DrawSprites(On.GraphicsModule.orig_DrawSprites orig, GraphicsModule graphicsModule,
			RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
		{
			orig(graphicsModule, sLeaser, rCam, timeStacker, camPos);


			if (graphicsModule.owner is Creature creature)
			{
				if (creature.Module.StalwartShellAbility)
				{
					Shell Shell = creature.Shell;


					if (!Shell.validity)
					{
						foreach (var sprite in Shell.sprites)
						{
							if (sprite != null)
							{
								sprite.RemoveFromContainer();
							}
						}
					}
					for (int j = 0; j < Shell.sprites.GetLength(0); j++)
					{
						for (int i = 0; i < Shell.sprites.GetLength(1); i++)
						{
							if (Shell.sprites[j, i] != null)
							{
								//Shell.sprites[i]?.x = graphicsModule.owner.bodyChunks[i].pos.x - camPos.x;
								//Shell.sprites[i]?.y = graphicsModule.owner.bodyChunks[i].pos.y - camPos.y;


								Shell.sprites[j, i]!.x = sLeaser.sprites[i].x;
								Shell.sprites[j, i]!.y = sLeaser.sprites[i].y;

								Shell.sprites[j, i]!.rotation = sLeaser.sprites[i].rotation;

								Shell.sprites[j, i]!.isVisible = sLeaser.sprites[i].isVisible;
								Shell.sprites[j, i]!.alpha = sLeaser.sprites[i].alpha;


								Shell.sprites[j, i]!.scaleX = (sLeaser.sprites[i].scaleX * 1.4f) - (Shell.damage * 0.425f);
								Shell.sprites[j, i]!.scaleY = (sLeaser.sprites[i].scaleY * 1.4f) - (Shell.damage * 0.425f);
								if (j == 0)
								{
									Shell.sprites[j, i]!.scaleX *= 1.175f - (Shell.damage * 0.2f);
									Shell.sprites[j, i]!.scaleY *= 1.175f - (Shell.damage * 0.2f);
									if (creature is Scavenger)
									{
										Shell.sprites[j, i]!.scaleX *= 1.475f - (Shell.damage * 0.5f);
										Shell.sprites[j, i]!.scaleY *= 1.475f - (Shell.damage * 0.5f);
									}
								}
							}
						}
					}
				}
			}
		}

		public static FSprite CloneFSprite(FSprite src)
		{
			// 用同一个图集元素和 facet 类型创建新 sprite
			var copy = new FSprite(src._element, src._facetTypeQuad)
			{
				// 复制位置、缩放、旋转、锚点
				_x = src._x,
				_y = src._y,
				_scaleX = src._scaleX,
				_scaleY = src._scaleY,
				_rotation = src._rotation,
				_anchorX = src._anchorX,
				_anchorY = src._anchorY,

				// 复制颜色和透明度
				_color = src._color,
				_alpha = src._alpha,

				// 复制可见性
				_isVisible = src._isVisible,
				_visibleScale = src._visibleScale,

				// 可选：复制 meshZ / sortZ
				_meshZ = src._meshZ,
				_sortZ = src._sortZ,

				// 可选：复制自定义数据
				// copy.data = src.data;

				// 标记为脏，让下一帧重新计算顶点、矩阵和颜色
				_isMatrixDirty = true,
				_isAlphaDirty = true,
				_areLocalVerticesDirty = true,
				_isMeshDirty = true
			};

			return copy;
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



		public class Shell
		{
			public bool validity = true;
			public float damage;
			public Vector2 lastHitOffset;

			public FSprite?[,] sprites = new FSprite?[2,0];

			public Shell(Creature creature)
			{

				if (Plugin.DebugMode)
				{
					//if (creature is Player)
					//{
					//	damage = -99999;
					//}
				}
			}

			~Shell()
			{
				foreach (var sprite in sprites)
				{
					if (sprite != null)
					{
						sprite.RemoveFromContainer();
					}
				}
			}
		}
		extension(Creature creature)
		{
			public Shell Shell => ModuleManager.Get(creature, c => new Shell(c));
		}

	}
}
