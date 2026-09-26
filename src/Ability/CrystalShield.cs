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
using System.Drawing;
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
using Color = UnityEngine.Color;
using Random = UnityEngine.Random;

namespace MySlugcat.Ability
{
	// 水晶盾
	public static class CrystalShield
	{
		public static bool CrystalShield_HitSomething<O, W>(O orig_, W weapon, SharedPhysics.CollisionResult result, bool eu)
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
				if (creature.Module.CrystalShieldAbility && creature.Shield.validity)
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

					creature.Shield.lastHitOffset = weapon.firstChunk.pos - creature.mainBodyChunk.pos;
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
			if (creature.Module.CrystalShieldAbility)
			{
				var Shield = creature.Shield;

				// 外观对象：不存在 / 已死 / 换房间时重建
				if ((Shield.graphics == null || Shield.graphics.slatedForDeletetion || Shield.graphics.room != creature.room)
					&& creature.room != null && Shield.validity)
				{
					Shield.graphics = new CrystalShieldGraphics(creature);
					creature.room.AddObject(Shield.graphics);
				}


				bool lastValidity = Shield.validity;
				if (Shield.validity)
				{
					//if (!Shield.initRestistances)
					//{
					//	Shield.initRestistances = true;

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

				if (Shield.damage >= 0.9f && Shield.validity && UnityEngine.Random.value < 0.015f)
				{
					Shatter(creature, Shield.lastHitOffset + creature.mainBodyChunk.pos);
				}

				//if (lastValidity && !Shield.validity)
				//{
				//	if (Shield.origTemplate != null)
				//	{
				//		creature.abstractCreature.creatureTemplate = Shield.origTemplate;
				//	}
				//}

				if (Plugin.DebugMode)
				{
					if (creature is Player)
					{
						if (Input.GetKey("x"))
						{
							Shield.damage = 0;
							Shield.validity = true;
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
			var Shield = creature.Shield;

			Shield.damage += damage * 0.2f;
			if (Shield.damage > 1)
				Shield.damage = 1;
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

			//	creature.room.AddObject(new CentipedeShield(firstChunk.pos, Custom.RNV() * Rand * 15 + extraVel, Abstr.hue, Abstr.saturation, 0.25f, 0.25f));
			//}

			creature.room.PlaySound(SoundID.Weapon_Skid, impactPos, 0.75f, 1.25f);

			creature.Shield.graphics?.Shatter(impactPos);
			creature.Shield.validity = false;
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



		public class Shield
		{
			public bool validity = true;
			public float damage;
			public Vector2 lastHitOffset;

			public CrystalShieldGraphics? graphics;

			public Shield(Creature creature)
			{

				if (Plugin.DebugMode)
				{
					//if (creature is Player)
					//{
					//	damage = -99999;
					//}
				}
			}
		}
		extension(Creature creature)
		{
			public Shield Shield => ModuleManager.Get(creature, c => new Shield(c));
		}


		public class CrystalShieldGraphics : CosmeticSprite
		{
			private readonly Creature creature;
			private Shield Shield => creature.Shield;

			private readonly List<Plate> plates = [];

			private float rotation;
			private float lastRotation;
			private float breathe;
			private int breakCooldown;

			public CrystalShieldGraphics(Creature creature)
			{
				this.creature = creature;
				this.rotation = Random.value * 360f;
				this.lastRotation = rotation;

				// 节数多的生物（蜈蚣之类）每块少挂几片，避免刷屏
				int perChunk = creature.bodyChunks.Length > 4 ? 4 : 6;
				for (int c = 0; c < creature.bodyChunks.Length; c++)
				{
					BodyChunk chunk = creature.bodyChunks[c];
					float startAngle = Random.value * 360f;
					for (int i = 0; i < perChunk; i++)
					{
						plates.Add(new Plate(chunk, startAngle + (360f / perChunk * i)));
					}
				}
			}

			// ---------- 结构 ----------

			private class Plate
			{
				public BodyChunk chunk;
				public float baseAngle;     // 环绕相位
				public float breakAt;       // 该板的损伤阈值 (0~1 随机)，达到即剥落
				public float phase;         // 呼吸/闪烁用的随机相位
				public float radX, radY;    // 环绕椭圆半径
				public float size;          // 板尺寸
				public TriangleMesh mesh;
				public bool broken;

				public Plate(BodyChunk chunk, float baseAngle)
				{
					this.chunk = chunk;
					this.baseAngle = baseAngle;
					this.breakAt = Random.value;
					this.phase = Random.value * Mathf.PI * 2f;
					this.radX = chunk.rad * 1.5f;
					this.radY = chunk.rad * 1.25f;
					this.size = Mathf.Max(4f, chunk.rad * 0.6f);
				}
			}

			private static TriangleMesh MakeHexMesh()
			{
				// 7 顶点：0~5 为边缘，6 为中心；扇形 6 个三角形
				var tris = new TriangleMesh.Triangle[6];
				for (int i = 0; i < 6; i++)
					tris[i] = new TriangleMesh.Triangle(6, i, (i + 1) % 6);
				return new TriangleMesh("Futile_White", tris, true);
			}

			// ---------- 生命周期 ----------

			public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
			{
				sLeaser.sprites = new FSprite[plates.Count];
				for (int i = 0; i < plates.Count; i++)
				{
					plates[i].mesh = MakeHexMesh();
					sLeaser.sprites[i] = plates[i].mesh;
				}

				AddToContainer(sLeaser, rCam, null);
			}
			public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer? newContatiner)
			{
				if (newContatiner == null)
				{
					newContatiner = rCam.ReturnFContainer("Midground");
				}
				foreach (FSprite fsprite in sLeaser.sprites)
				{
					fsprite.RemoveFromContainer();
					newContatiner.AddChild(fsprite);
				}
			}

			public override void Update(bool eu)
			{
				base.Update(eu);

				if (creature.slatedForDeletetion || creature.room == null || creature.room != room)
				{
					slatedForDeletetion = true;
					return;
				}

				lastRotation = rotation;
				rotation += 0.35f;          // 壳体缓慢自转
				breathe += 0.075f;          // 呼吸节奏

				// 逐块剥落：每 2 帧最多碎一块，形成连锁剥落感
				if (breakCooldown > 0)
				{
					breakCooldown--;
				}
				else
				{
					foreach (var plate in plates)
					{
						if (!plate.broken && Shield.damage >= plate.breakAt)
						{
							plate.broken = true;
							breakCooldown = 2;
							Vector2 p = PlatePos(plate, 1f);
							SpawnDebris(p, Custom.RNV() * Mathf.Lerp(2f, 5f, Random.value));
							room.PlaySound(SoundID.Weapon_Skid, p, 0.3f, 1.7f + (Random.value * 0.5f));
							break;
						}
					}
				}
			}

			public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam,
				float timeStacker, Vector2 camPos)
			{
				base.DrawSprites(sLeaser, rCam, timeStacker, camPos);
				if (slatedForDeletetion) return;

				float rot = Mathf.Lerp(lastRotation, rotation, timeStacker);
				Color baseCol = Color.Lerp(creature.ShortCutColor(), new Color(0.85f, 0.72f, 0.42f), 0.55f);

				foreach (var plate in plates)
				{
					var mesh = plate.mesh;
					if (plate.broken)
					{
						for (int i = 0; i < 7; i++) mesh.verticeColors[i] = new Color(0f, 0f, 0f, 0f);
						continue;
					}

					float a = plate.baseAngle + rot;
					Vector2 dir = Custom.DegToVec(a);
					float depth = Mathf.Sin(a * Mathf.Deg2Rad);   // -1 背侧 ~ +1 腹前，伪 3D

					Vector2 chunkPos = Vector2.Lerp(plate.chunk.lastPos, plate.chunk.pos, timeStacker);
					Vector2 center = chunkPos + new Vector2(dir.x * plate.radX, dir.y * plate.radY);

					float pulse = 1f + (0.03f * Mathf.Sin(breathe + plate.phase));
					float size = plate.size * (0.85f + (0.18f * depth)) * pulse;

					for (int k = 0; k < 6; k++)
						mesh.MoveVertice(k, center + (Custom.DegToVec(a + (k * 60f)) * size) - camPos);
					mesh.MoveVertice(6, center - (dir * size * 0.2f) - camPos); // 中心内凹，伪造球面

					// 明暗：背侧暗、前侧亮；中心高光
					float shade = 0.7f + (0.35f * ((depth * 0.5f) + 0.5f));
					Color rim = baseCol * shade;
					Color mid = Color.Lerp(baseCol * (shade + 0.15f), Color.white, 0.3f);
					for (int k = 0; k < 6; k++) mesh.verticeColors[k] = rim;
					mesh.verticeColors[6] = mid;
				}
			}

			public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
			{
				// 暗房整体压暗
				foreach (var plate in plates)
				{
					if (plate.mesh == null) continue;
					plate.mesh.alpha = Mathf.Lerp(1f, 0.55f, palette.darkness);
				}
			}

			// 整壳爆裂（被 StalwartShell.Shatter 调用）
			public void Shatter(Vector2 impactPos)
			{
				foreach (var plate in plates)
				{
					if (plate.broken) continue;
					plate.broken = true;
					Vector2 p = PlatePos(plate, 1f);
					Vector2 burst = Custom.DirVec(impactPos, p) * Mathf.Lerp(4f, 10f, Random.value);
					SpawnDebris(p, burst);
				}
				room.PlaySound(SoundID.Weapon_Skid, impactPos, 0.8f, 1.2f);
				slatedForDeletetion = true;
			}

			private Vector2 PlatePos(Plate plate, float timeStacker)
			{
				float a = plate.baseAngle + rotation;
				Vector2 dir = Custom.DegToVec(a);
				Vector2 chunkPos = Vector2.Lerp(plate.chunk.lastPos, plate.chunk.pos, timeStacker);
				return chunkPos + new Vector2(dir.x * plate.radX, dir.y * plate.radY);
			}

			private void SpawnDebris(Vector2 pos, Vector2 vel)
			{
				Color baseCol = Color.Lerp(creature.ShortCutColor(), new Color(0.85f, 0.72f, 0.42f), 0.55f);
				Color.RGBToHSV(baseCol, out float h, out float s, out float v);

				// 琥珀色甲壳碎屑，带物理反弹
				room.AddObject(new CentipedeShell(
					pos, vel + (Custom.RNV() * Random.value * 7f),
					//0.085f + (Random.value * 0.03f), 0.55f + (Random.value * 0.2f),
					h, 0.55f + (Random.value * 0.2f),
					0.28f, 0.32f));

				for (int i = 0; i < 4; i++)
				{
					room.AddObject(new Spark(
						pos + (Custom.RNV() * 4f),
						Custom.RNV() * Mathf.Lerp(2f, 6f, Random.value),
						new Color(1f, 1f, 1f), null, 8, 120));
				}
			}
		}

	}
}
