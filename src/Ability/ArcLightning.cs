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
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using Watcher;
using static Menu.Remix.InternalOI;
using static MonoMod.InlineRT.MonoModRule;
using static MySlugcat.Ability.Hardening;

namespace MySlugcat.Ability
{
	// 电弧连锁
	public static class ArcLightning
	{
		public static void Player_Update(On.Player.orig_Update orig, Player player, bool eu)
		{
			orig(player, eu);

			player.GetModule(out var playerModule);
			if (playerModule.ArcLightningAbility)
			{
				player.GetArcLightningModule(out var module);

				if (player.room == null || !player.Consious)
				{
					module.chargedAuraArcs.Clear();
					return;
				}

				UpdateChargedAuraPositions(player);

				module.chargedAuraTimer--;
				if (module.chargedAuraTimer <= 0)
				{
					SpawnChargedAuraBurst(player);
					module.chargedAuraTimer = UnityEngine.Random.Range(28, 49);
				}

				if (UnityEngine.Random.value < 0.025f)
				{
					Spark(player);
				}
			}
		}

		public static void Weapon_Thrown(On.Weapon.orig_Thrown orig, Weapon weapon, Creature thrownBy, Vector2 thrownPos,
			Vector2? firstFrameTraceFromPos, IntVector2 throwDir, float frc, bool eu)
		{
			orig(weapon, thrownBy, thrownPos, firstFrameTraceFromPos, throwDir, frc, eu);

			if (thrownBy is Player player)
			{
				player.GetModule(out var playerModule);
				if (playerModule.ArcLightningAbility)
				{
					Spark(thrownBy);
				}
			}
		}

		#region Module
		public class ArcLightningModule
		{
			public readonly List<ChargedAuraArc> chargedAuraArcs = [];
			public int chargedAuraTimer;
		}
		public static ArcLightningModule GetArcLightningModule(this Creature creature, out ArcLightningModule module)
		{
			module = GetArcLightningModule(creature);
			return module;
		}
		public static ArcLightningModule GetArcLightningModule(this Creature creature)
		{
			return ModuleManager.Get(creature, c => new ArcLightningModule());
		}
		#endregion
		#region ChargedAuraArc
		public sealed class ChargedAuraArc
		{
			internal LightningBolt? Bolt;
			internal Vector2 Axis;
		}
		private static void SpawnChargedAuraBurst(Creature creature)
		{
			creature.GetArcLightningModule(out var module);

			int num = (UnityEngine.Random.value < 0.28f) ? 2 : 1;
			for (int i = 0; i < num; i++)
			{
				Vector2 axis = Custom.RNV();
				GetBodyBoundedArc(creature, axis, out var start, out var end);

				float lightningType = UnityEngine.Random.Range(0.585f, 0.635f);

				//this.lifeTime = lifeTime * 30f;
				LightningBolt lightningBolt = new LightningBolt(start, end, 0,
					UnityEngine.Random.Range(0.45f, 0.62f), UnityEngine.Random.Range(0.48f, 0.72f), 1f, lightningType, true)
				{
					intensity = UnityEngine.Random.Range(1.3f, 1.55f)
				};
				creature.room.AddObject(lightningBolt);

				module.chargedAuraArcs.Add(new ChargedAuraArc
				{
					Bolt = lightningBolt,
					Axis = axis
				});
			}

			Vector2 vector4 = creature.mainBodyChunk.pos;
			if (creature.bodyChunks.Length >= 3)
			{
				vector4 = Vector2.Lerp(creature.bodyChunks[0].pos, creature.bodyChunks[2].pos, 0.5f);
			}
			else if (creature.bodyChunks.Length >= 2)
			{
				vector4 = Vector2.Lerp(creature.bodyChunks[0].pos, creature.bodyChunks[1].pos, 0.5f);
			}
			creature.room.PlaySound(SoundID.Death_Lightning_Spark_Spontaneous, vector4, 0.32f, UnityEngine.Random.Range(1.05f, 1.35f));
		}

		private static void GetBodyBoundedArc(Creature creature, Vector2 axis, out Vector2 start, out Vector2 end)
		{
			axis.Normalize();
			float num = float.MaxValue;
			float num2 = float.MinValue;
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			for (int i = 0; i < creature.bodyChunks.Length; i++)
			{
				BodyChunk bodyChunk = creature.bodyChunks[i];
				num = Mathf.Min(num, bodyChunk.pos.x - bodyChunk.rad);
				num2 = Mathf.Max(num2, bodyChunk.pos.x + bodyChunk.rad);
				num3 = Mathf.Min(num3, bodyChunk.pos.y - bodyChunk.rad);
				num4 = Mathf.Max(num4, bodyChunk.pos.y + bodyChunk.rad);
			}
			Vector2 vector = new Vector2((num + num2) * 0.5f, (num3 + num4) * 0.5f);
			float num5 = Mathf.Max(18f, (((num2 - num) * 0.5f) + 12f) * 1.8f);
			float num6 = Mathf.Max(18f, (((num4 - num3) * 0.5f) + 12f) * 1.8f);
			float num7 = Mathf.Sqrt((axis.x * axis.x / (num5 * num5)) + (axis.y * axis.y / (num6 * num6)));
			float num8 = (num7 > 0.0001f) ? (1f / num7) : Mathf.Min(num5, num6);
			start = vector - (axis * num8);
			end = vector + (axis * num8);
		}

		private static void UpdateChargedAuraPositions(Creature creature)
		{
			creature.GetArcLightningModule(out var module);

			for (int i = module.chargedAuraArcs.Count - 1; i >= 0; i--)
			{
				ChargedAuraArc chargedAuraArc = module.chargedAuraArcs[i];
				if (chargedAuraArc.Bolt == null || chargedAuraArc.Bolt.slatedForDeletetion)
				{
					module.chargedAuraArcs.RemoveAt(i);
				}
				else
				{
					GetBodyBoundedArc(creature, chargedAuraArc.Axis, out var start, out var end);
					chargedAuraArc.Bolt.from = start;
					chargedAuraArc.Bolt.target = end;
				}
			}
		}
		#endregion



		public static bool ArcLightning_HitSomething<O, W>(O orig_, W weapon, SharedPhysics.CollisionResult result, bool eu)
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

			weapon.GetModule(out var weaponModule);
			if (weaponModule.Owner.TryGetTarget(out var target) && target is Player player)
			{
				if (player.GetModule().ArcLightningAbility)
				{
					if (result.obj is Creature hitCreature)
					{
						weaponModule.stuckInObject.TryGetTarget(out var stuckInObject);
						if (stuckInObject != hitCreature || weaponModule.stuckInObjectTime > 30)
						{
							if (weapon is not Spear && player.GetModule().DeflagrationAbility && false) // false
							{
								Log.LogInfo($"不触发");
							}
							else
							{
								List<Creature> exclude = [player];
								// 执行连锁
								ArcTriggerChain(weapon, hitCreature, player, weapon.firstChunk.vel.normalized, ref exclude, 5);
							}
						}
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


		public static float ChainRadius = 14f * 20f; // 14格
		public static int MaxTargets = 5;
		public static float Damage = 0.2f;
		public static float StunBonus = 60f;
		public static float ConeHalfAngle = 60f;           // 半角，总角度为120°

		private static void ArcTriggerChain(Weapon weapon, Creature start, Player player, Vector2 direction, ref List<Creature> exclude, int remainingChains)
		{
			if (remainingChains <= 0) return;

			Room room = start.room;
			if (room == null) return;

			room.PlaySound(SoundID.Jelly_Fish_Tentacle_Stun, start.firstChunk);

			SpawnChargedAuraBurst(start);
			UpdateChargedAuraPositions(start);


			Vector2 startPos = start.mainBodyChunk.pos;
			direction = direction.normalized;

			List<Creature> candidates = Helper.FindCreaturesInCone(startPos, direction, room,
				ConeHalfAngle, ChainRadius, exclude, null, true);

			if (candidates.Count == 0) return;
			// 按距离排序
			candidates.Sort((a, b) =>
			{
				float da = Vector2.Distance(startPos, a.mainBodyChunk.pos);
				float db = Vector2.Distance(startPos, b.mainBodyChunk.pos);
				return da.CompareTo(db);
			});



			int count = Math.Min(Math.Min(candidates.Count, UnityEngine.Random.Range(1, 4)), remainingChains);

			for (int i = 0; i < count; i++)
			{
				Creature target = candidates[i];
				Vector2 targetPos = target.mainBodyChunk.pos;
				exclude.Add(target);


				bool isElectricCreature = CheckElectricCreature(start);
				if (isElectricCreature || UnityEngine.Random.value < 0.025f)
				{
					Recharge(weapon, start, target, player);
					remainingChains += 3;
				}


				float stunBonus = (target is not Player) ? (120f * Mathf.Lerp(target.Template.baseStunResistance, 1f, 0.5f)) : 80f;
				if (target is not BigEel && !isElectricCreature)
				{
					// 施加电击伤害和眩晕
					target.Violence(player.firstChunk,
						new Vector2?(Custom.DirVec(start.firstChunk.pos, target.firstChunk.pos) * 5f),
						target.firstChunk,
						null,
						Creature.DamageType.Electric,
						0.1f,
						stunBonus);

					room.AddObject(new CreatureSpasmer(target, false, target.stun));


					//target.Violence(player.firstChunk,
					//	new Vector2?(weapon.firstChunk.vel * weapon.firstChunk.mass * 0.5f),
					//	target.mainBodyChunk,
					//	null,
					//	Creature.DamageType.Electric,
					//	Damage,
					//	StunBonus);
				}

				// 视觉特效
				room.AddObject(new LightningLine(start, target, stunBonus));
				SpawnLightningEffect(room, startPos, targetPos);
				//room.AddObject(new ExplosionSpikes(room, target.mainBodyChunk.pos, 8, 20f, 5f, 5f, 120f, target.ShortCutColor()));


				direction = (targetPos - startPos).normalized;

				for (int j = 0; j < Mathf.Pow(UnityEngine.Random.value, 4f) * 3; j++)
				{
					ArcTriggerChain(weapon, target, player, direction, ref exclude, remainingChains - 1);
				}
			}
		}

		// 生成电弧特效（简单火花线）
		private static void SpawnLightningEffect(Room room, Vector2 from, Vector2 to)
		{
			int steps = 10;
			for (int i = 0; i <= steps; i++)
			{
				float t = i / (float)steps;
				Vector2 pos = Vector2.Lerp(from, to, t);
				// 加一点随机偏移，更像电弧
				pos += Custom.RNV() * 2f;
				room.AddObject(new Spark(pos, Custom.RNV() * 3f, Color.white, null, 6, 30));
			}
		}

		public static void Recharge(Weapon weapon, Creature start, Creature target, Player player)
		{
			Room room = target.room;

			room.PlaySound(SoundID.Jelly_Fish_Tentacle_Stun, target.firstChunk);
			room.AddObject(new Explosion.ExplosionLight(target.firstChunk.pos, 200f, 1f, 4, new Color(0.7f, 1f, 1f)));
			Spark(target);
			Zap(target, player);
			room.AddObject(new ZapCoil.ZapFlash(target.firstChunk.pos, 25f));
		}
		public static void Spark(Creature target)
		{
			Room room = target.room;

			//if (base.abstractSpear.electricCharge == 0)
			//{
			//	return;
			//}
			for (int i = 0; i < 10; i++)
			{
				Vector2 vector = Custom.RNV();
				room.AddObject(new Spark(target.firstChunk.pos + (vector * (UnityEngine.Random.value * 20f)),
					vector * Mathf.Lerp(4f, 10f, UnityEngine.Random.value),
					Color.white, null, 4, 18));
			}
		}
		public static void Zap(Creature target, Creature thrownBy)
		{
			Room room = target.room;

			float zapPitch = 4f + (UnityEngine.Random.value * 3f);

			//if (base.abstractSpear.electricCharge == 0)
			//{
			//    return;
			//}
			room.AddObject(new ZapCoil.ZapFlash(target.firstChunk.pos, 10f));
			room.PlaySound(SoundID.Zapper_Zap, target.firstChunk, false, 1f, (zapPitch == 0f) ? (1.5f + (UnityEngine.Random.value * 1.5f)) : zapPitch);
			if (target.Submersion > 0.5f)
			{
				room.AddObject(new UnderwaterShock(room, null, target.firstChunk.pos, 10, 800f, 2f, thrownBy, new Color(0.8f, 0.8f, 1f)));
			}
		}

		public static bool CheckElectricCreature(Creature otherObject)
		{
			return otherObject is Centipede || otherObject is BigJellyFish || otherObject is Inspector;
		}



		public class LightningLine : CosmeticSprite
		{
			public WeakReference<Creature> start;
			public WeakReference<Creature> target;

			private Vector2 startPos;
			private Vector2 lastStartPos;
			private Vector2 targetPos;
			private Vector2 lastTargetPos;

			private float life;
			private float lastLife;
			private float lifeTime;

			// ==== 电弧动画参数 ====
			private const int SegmentCount = 10;     // 电弧段数（线段数量）
			private const float BaseThickness = 2.5f;   // 核心线宽（像素）
			private const float GlowThickness = 7f;     // 光晕线宽（像素）
			private const float JitterAmount = 7f;     // 垂直抖动幅度（像素）
			private const int RegenInterval = 2;      // 每 N 帧重新生成一次抖动目标
			private const float OffsetSmooth = 0.35f;  // 抖动插值系数

			// 每个节点的垂直偏移，0 号是起点，SegmentCount 号是终点
			private float[] offsets;        // 当前偏移
			private float[] lastOffsets;    // 上一帧偏移
			private float[] targetOffsets;  // 目标偏移
			private int regenCounter;
			private float flickerSeed;

			// 精灵缓存
			private FSprite[] glowSprites = [];
			private FSprite[] coreSprites = [];

			public LightningLine(Creature start, Creature target, float lifeTime)
			{
				this.start = new WeakReference<Creature>(start);
				this.target = new WeakReference<Creature>(target);

				this.startPos = start.mainBodyChunk.pos;
				this.lastStartPos = start.mainBodyChunk.lastPos;
				this.targetPos = target.mainBodyChunk.pos;
				this.lastTargetPos = target.mainBodyChunk.lastPos;

				this.lifeTime = lifeTime;
				this.life = 0f;
				this.lastLife = 0f;

				int pointCount = SegmentCount + 1;
				offsets = new float[pointCount];
				lastOffsets = new float[pointCount];
				targetOffsets = new float[pointCount];

				GenerateTargetOffsets();

				flickerSeed = UnityEngine.Random.value * 100f;
				regenCounter = 0;
			}

			/// <summary>生成一次"锯齿"目标偏移。端点保持 0，中间点用正弦包络约束，最后去均值避免整体歪斜。</summary>
			private void GenerateTargetOffsets()
			{
				int pointCount = SegmentCount + 1;
				for (int i = 0; i < pointCount; i++)
				{
					if (i == 0 || i == pointCount - 1)
					{
						targetOffsets[i] = 0f;
						continue;
					}
					float t = i / (float)(pointCount - 1);
					float taper = Mathf.Sin(t * Mathf.PI); // 两端小、中间大
					targetOffsets[i] = UnityEngine.Random.Range(-JitterAmount, JitterAmount) * taper;
				}

				// 去均值，避免整条线朝一侧弯
				float sum = 0f;
				for (int i = 0; i < pointCount; i++) sum += targetOffsets[i];
				float avg = sum / pointCount;
				for (int i = 0; i < pointCount; i++) targetOffsets[i] -= avg;
			}

			public override void Update(bool eu)
			{
				base.Update(eu);

				lastLife = life;
				life += 1f / lifeTime;

				if (lastLife > 1f)
				{
					Destroy();
					return;
				}

				if (start.TryGetTarget(out Creature startCreature))
				{
					lastStartPos = startPos;
					startPos = startCreature.mainBodyChunk.pos;
				}

				if (target.TryGetTarget(out Creature targetCreature))
				{
					lastTargetPos = targetPos;
					targetPos = targetCreature.mainBodyChunk.pos;
				}

				// 偏移向目标平滑靠拢
				for (int i = 0; i < offsets.Length; i++)
				{
					lastOffsets[i] = offsets[i];
					offsets[i] = Mathf.Lerp(offsets[i], targetOffsets[i], OffsetSmooth);
				}

				// 周期性抖动
				regenCounter++;
				if (regenCounter >= RegenInterval)
				{
					regenCounter = 0;
					GenerateTargetOffsets();
				}
			}

			public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
			{
				int n = SegmentCount;
				glowSprites = new FSprite[n];
				coreSprites = new FSprite[n];

				sLeaser.sprites = new FSprite[n * 2];

				for (int i = 0; i < n; i++)
				{
					FSprite glow = new FSprite("Futile_White")
					{
						anchorX = 0f,
						anchorY = 0.5f,
						color = new Color(0.4f, 0.75f, 1f),
						alpha = 0.5f,
						//shader = rCam.game.rainWorld.Shaders["Additive"],
					};
					FSprite core = new FSprite("Futile_White")
					{
						anchorX = 0f,
						anchorY = 0.5f,
						color = Color.white,
					};

					glowSprites[i] = glow;
					coreSprites[i] = core;

					sLeaser.sprites[i] = glow;
					sLeaser.sprites[n + i] = core;
				}

				AddToContainer(sLeaser, rCam, null);
			}

			public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
			{
				base.DrawSprites(sLeaser, rCam, timeStacker, camPos);

				int n = SegmentCount;
				int pointCount = n + 1;

				// 端点失效则隐藏所有精灵
				if (!start.TryGetTarget(out _) || !target.TryGetTarget(out _))
				{
					for (int i = 0; i < sLeaser.sprites.Length; i++)
						sLeaser.sprites[i].isVisible = false;
					return;
				}

				// 两端位置插值
				Vector2 interpStart = Vector2.Lerp(lastStartPos, startPos, timeStacker);
				Vector2 interpTarget = Vector2.Lerp(lastTargetPos, targetPos, timeStacker);

				Vector2 dir = interpTarget - interpStart;
				float len = dir.magnitude;
				if (len < 0.01f)
				{
					for (int i = 0; i < sLeaser.sprites.Length; i++)
						sLeaser.sprites[i].isVisible = false;
					return;
				}
				Vector2 dirNorm = dir / len;
				Vector2 perp = new Vector2(-dirNorm.y, dirNorm.x);

				// 生命周期 & 闪烁
				float interpLife = Mathf.Lerp(lastLife, life, timeStacker);
				float fadeAlpha = Mathf.Clamp01(1f - interpLife);

				float flicker = 0.75f + (Mathf.PerlinNoise(Time.time * 30f, flickerSeed) * 0.5f);
				if (interpLife > 0.7f)
				{
					// 消散时随机快速闪烁
					flicker *= (UnityEngine.Random.value > 0.5f) ? 1f : 0.3f;
				}
				float alpha = Mathf.Clamp01(fadeAlpha * flicker);

				// 计算所有节点
				Vector2[] pts = new Vector2[pointCount];
				for (int i = 0; i < pointCount; i++)
				{
					float t = i / (float)(pointCount - 1);
					Vector2 straight = Vector2.Lerp(interpStart, interpTarget, t);
					float off = Mathf.Lerp(lastOffsets[i], offsets[i], timeStacker);
					pts[i] = straight + (perp * off);
				}

				// 颜色随生命从白 → 淡蓝
				Color coreColor = Color.Lerp(Color.white, new Color(0.6f, 0.8f, 1f), interpLife);

				// 更新每段
				for (int i = 0; i < n; i++)
				{
					Vector2 a = pts[i];
					Vector2 b = pts[i + 1];
					Vector2 seg = b - a;
					float segLen = seg.magnitude;

					FSprite glow = glowSprites[i];
					FSprite core = coreSprites[i];

					if (segLen < 0.01f)
					{
						glow.isVisible = false;
						core.isVisible = false;
						continue;
					}

					glow.isVisible = true;
					core.isVisible = true;

					float angle = Custom.VecToDeg(seg / segLen) - 90f;

					// 光晕（宽、半透明、加色）
					glow.x = a.x - camPos.x;
					glow.y = a.y - camPos.y;
					glow.scaleX = segLen / 16f;
					glow.scaleY = GlowThickness / 16f;
					glow.rotation = angle;
					glow.alpha = alpha * 0.55f;

					// 核心（细、亮）
					core.x = a.x - camPos.x;
					core.y = a.y - camPos.y;
					core.scaleX = segLen / 16f;
					core.scaleY = BaseThickness / 16f;
					core.rotation = angle;
					core.alpha = alpha;
					core.color = coreColor;
				}
			}

			public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer? newContatiner)
			{
				if (newContatiner == null)
				{
					newContatiner = rCam.ReturnFContainer("HUD");
				}

				foreach (FSprite fsprite in sLeaser.sprites)
				{
					fsprite.RemoveFromContainer();
					newContatiner.AddChild(fsprite);
				}
			}
		}

	}
}
