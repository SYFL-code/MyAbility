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
using Color = UnityEngine.Color;
using Random = UnityEngine.Random;

namespace MySlugcat.Ability
{
	// 电弧连锁
	public static class ArcLightning
	{
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
			if (weaponModule.Owner.TryGetTarget(out var target) && target is Creature thrownBy)
			{
				if (thrownBy.GetModule().ArcLightningAbility)
				{
					if (result.obj is Creature hitCreature)
					{
						if (weapon is Weapon)
						{
							List<Creature> exclude = [thrownBy];
							// 执行连锁
							ArcTriggerChain(weapon, hitCreature, thrownBy, weapon.firstChunk.vel.normalized, ref exclude, 5);
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

		private static void ArcTriggerChain(Weapon weapon, Creature start, Creature thrownBy, Vector2 direction, ref List<Creature> exclude, int remainingChains)
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
				ConeHalfAngle, ChainRadius, exclude, [thrownBy.GetType(), typeof(Fly)], true);

			if (candidates.Count == 0) return;
			// 按距离排序
			candidates.SortDistance(startPos);


			int count = Math.Min(Math.Min(candidates.Count, UnityEngine.Random.Range(1, 4)), remainingChains);

			for (int i = 0; i < count; i++)
			{
				Creature target = candidates[i];
				Vector2 targetPos = target.mainBodyChunk.pos;
				exclude.Add(target);


				bool isElectricCreature = CheckElectricCreature(start);
				if (isElectricCreature || UnityEngine.Random.value < 0.025f)
				{
					Recharge(weapon, start, target, thrownBy);
					remainingChains += 3;
				}


				float stunBonus = (target is not Player) ? (120f * Mathf.Lerp(target.Template.baseStunResistance, 1f, 0.5f)) : 80f;
				if (target is not BigEel && !isElectricCreature)
				{
					// 施加电击伤害和眩晕
					target.Violence(thrownBy.firstChunk,
						Custom.DirVec(start.firstChunk.pos, target.firstChunk.pos) * 5f,
						target.firstChunk,
						null,
						Creature.DamageType.Electric,
						0.1f,
						stunBonus);

					//target.Violence(player.firstChunk,
					//	new Vector2?(weapon.firstChunk.vel * weapon.firstChunk.mass * 0.5f),
					//	target.mainBodyChunk,
					//	null,
					//	Creature.DamageType.Electric,
					//	Damage,
					//	StunBonus);

					target.GetArcLightningModule(out var module);
					if (module.creatureSpasmer == null || module.creatureSpasmer.slatedForDeletetion)
					{
						module.creatureSpasmer = new CreatureSpasmer(target, true, target.stun);
						room.AddObject(module.creatureSpasmer);
					}
					else
					{
						module.creatureSpasmer.counter = target.stun;
					}
				}
				if (weapon.Submersion <= 0.5f && start.Submersion > 0.5f)
				{
					room.AddObject(new UnderwaterShock(room, null, start.firstChunk.pos, 10, 800f, 2f, weapon.thrownBy, new Color(0.8f, 0.8f, 1f)));
				}

				// 视觉特效
				room.AddObject(new LightningLine(start, target, 4f));
				SpawnLightningEffect(room, startPos, targetPos);
				//room.AddObject(new ExplosionSpikes(room, target.mainBodyChunk.pos, 8, 20f, 5f, 5f, 120f, target.ShortCutColor()));


				direction = (targetPos - startPos).normalized;

				for (int j = 0; j < Mathf.Pow(UnityEngine.Random.value, 4f) * 3; j++)
				{
					ArcTriggerChain(weapon, target, thrownBy, direction, ref exclude, remainingChains - 1);
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

		public static void Recharge(Weapon weapon, Creature start, Creature target, Creature player)
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

			//room.AddObject(new LightningRing(target, 18f));
			//for (int i = 0; i < target.bodyChunks.Length; i++)
			//{
			//	Vector2 pos = target.bodyChunks[i].pos;
			//	for (int j = 0; j < 8; j++)
			//	{
			//		Vector2 dir = Custom.DegToVec(j * 45f);
			//		room.AddObject(new Spark(pos + (dir * 5f), dir * 0.1f, Color.blue, null, 5, UnityEngine.Random.Range(8, 12)));
			//	}
			//}


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
			// 蜈蚣 大水母 督察
			return otherObject is Centipede || otherObject is BigJellyFish || otherObject is Inspector;
		}



		public static void Creature_Update(On.Creature.orig_Update orig, Creature creature, bool eu)
		{
			orig(creature, eu);

			if (creature.GetModule().ArcLightningAbility)
			{
				creature.GetArcLightningModule(out var module);

				if (creature.room == null || !creature.Consious)
				{
					module.chargedAuraArcs.Clear();
					return;
				}

				//UpdateChargedAuraPositions(player);

				//module.chargedAuraTimer--;
				//if (module.chargedAuraTimer <= 0)
				//{
				//	SpawnChargedAuraBurst(player);
				//	module.chargedAuraTimer = UnityEngine.Random.Range(28, 49);
				//}


				if (UnityEngine.Random.value < 0.025f)
				{
					//creature.room.AddObject(new ElectricArcCosmetic(creature,
					//	radius: 35f, life: Random.Range(0.08f, 0.2f), width: Random.Range(1.5f, 3f)));
					creature.room.AddObject(new ElectricArcCosmetic(creature,
							radius: Random.Range(20f, 45f),
							lifeSeconds: Random.Range(0.06f, 0.18f),
							width: Random.Range(0.03f, 0.08f),
							hue: 0.6f,
							chunk: Random.Range(0, creature.bodyChunks.Length)));   // 躯干和臀部随机取锚点


					creature.room.PlaySound(SoundID.Death_Lightning_Spark_Spontaneous, creature.mainBodyChunk.pos, 0.32f, UnityEngine.Random.Range(1.05f, 1.35f));
					//Spark(player);
				}

				//for (int i = 0; i < player.grasps.Length; i++)
				//{
				//	if (player.grasps[i]?.grabbed is Weapon weapon)
				//	{
				//		if (UnityEngine.Random.value < 0.025f)
				//		{
				//			player.room.AddObject(new WeaponArcField(weapon, 80f));
				//		}
				//	}
				//}
			}
		}

		public static void Weapon_Thrown(On.Weapon.orig_Thrown orig, Weapon weapon, Creature thrownBy, Vector2 thrownPos,
			Vector2? firstFrameTraceFromPos, IntVector2 throwDir, float frc, bool eu)
		{
			orig(weapon, thrownBy, thrownPos, firstFrameTraceFromPos, throwDir, frc, eu);

			if (thrownBy.GetModule().ArcLightningAbility)
			{
				Spark(thrownBy);
			}
		}

		#region Module
		public class ArcLightningModule
		{
			public readonly List<ChargedAuraArc> chargedAuraArcs = [];
			public int chargedAuraTimer;

			public CreatureSpasmer? creatureSpasmer;
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
			if (creature is Player)
			{
				num5 = Mathf.Max(18f, (((num2 - num) * 0.5f) + 12f) * 1.2f);
				num6 = Mathf.Max(18f, (((num4 - num3) * 0.5f) + 12f) * 1.2f);
			}
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

			// ===== 电弧参数 =====
			private const int MainSegments = 10;    // 主链线段数
			private const int ForkSegments = 4;     // 分叉线段数
			private const float BaseThickness = 2.6f;  // 核心线宽
			private const float GlowThickness = 10f;   // 光晕线宽
			private const float JitterAmount = 9f;    // 主链抖动幅度
			private const float ForkJitter = 6f;    // 分叉抖动幅度
			private const float WalkDamp = 0.55f; // 随机游走衰减（越小越锐）
			private const int RegenInterval = 3;     // 每 N 帧换一次形状
			private const float StartRadius = 6f;    // 端点从体表推出去的距离
			private const float ForkLengthRatio = 0.32f; // 分叉长度 / 主链长
			private const float ForkChance = 0.75f; // 出现分叉的概率

			// 主链 / 分叉的垂直偏移
			private float[] offsets;
			private float[] forkOffsets;

			// 分叉状态
			private bool hasFork;
			private int forkParent;    // 从主链哪一段分叉
			private float forkDirBias;   // 分叉相对主链方向的偏转（垂直分量）

			private int regenCounter;
			private float flickerSeed;

			// 精灵缓存
			private FSprite[] glowSprites = [];      // 主链光晕
			private FSprite[] coreSprites = [];      // 主链核心
			private FSprite[] forkGlowSprites = [];  // 分叉光晕
			private FSprite[] forkCoreSprites = [];  // 分叉核心

			public LightningLine(Creature start, Creature target, float lifeTime)
			{
				this.start = new WeakReference<Creature>(start);
				this.target = new WeakReference<Creature>(target);

				this.startPos = start.mainBodyChunk.pos;
				this.lastStartPos = start.mainBodyChunk.lastPos;
				this.targetPos = target.mainBodyChunk.pos;
				this.lastTargetPos = target.mainBodyChunk.lastPos;

				// 建议传 6 ~ 10。闪电不像绳子，就是几帧的事
				this.lifeTime = Mathf.Max(1f, lifeTime);
				this.life = 0f;
				this.lastLife = 0f;

				int pointCount = MainSegments + 1;
				offsets = new float[pointCount];
				forkOffsets = new float[ForkSegments + 1];

				// 随机决定是否分叉
				hasFork = UnityEngine.Random.value < ForkChance;
				forkParent = UnityEngine.Random.Range(2, MainSegments - 2);
				forkDirBias = UnityEngine.Random.Range(-0.7f, 0.7f);

				RegenerateOffsets();

				flickerSeed = UnityEngine.Random.value * 100f;
				regenCounter = 0;
			}

			/// <summary>
			/// 生成一次折角偏移。用"随机游走 + 均值回归"，让相邻节点差值累计，
			/// 形成锐利的 V 形折角，同时避免整条线飘出屏幕。
			/// </summary>
			private void RegenerateOffsets()
			{
				int pointCount = MainSegments + 1;

				// 主链：端点固定为 0，中间随机游走
				offsets[0] = 0f;
				offsets[pointCount - 1] = 0f;
				float acc = 0f;
				for (int i = 1; i < pointCount - 1; i++)
				{
					acc += UnityEngine.Random.Range(-JitterAmount, JitterAmount) * 0.9f;
					acc *= WalkDamp;
					offsets[i] = acc;
				}

				// 分叉：同理，但从主链节点出发，起点固定为 0
				if (hasFork)
				{
					forkOffsets[0] = 0f;
					float facc = 0f;
					for (int i = 1; i <= ForkSegments; i++)
					{
						facc += UnityEngine.Random.Range(-ForkJitter, ForkJitter) * 0.9f;
						facc *= WalkDamp;
						forkOffsets[i] = facc;
					}
				}
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

				// 端点位置跟踪
				if (start.TryGetTarget(out Creature sc))
				{
					lastStartPos = startPos;
					startPos = sc.mainBodyChunk.pos;
				}
				if (target.TryGetTarget(out Creature tc))
				{
					lastTargetPos = targetPos;
					targetPos = tc.mainBodyChunk.pos;
				}

				// 周期性换形，直接覆盖（跳变，不插值）
				regenCounter++;
				if (regenCounter >= RegenInterval)
				{
					regenCounter = 0;
					RegenerateOffsets();
				}
			}

			public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
			{
				int total = (MainSegments + ForkSegments) * 2;
				sLeaser.sprites = new FSprite[total];

				glowSprites = new FSprite[MainSegments];
				coreSprites = new FSprite[MainSegments];
				forkGlowSprites = new FSprite[ForkSegments];
				forkCoreSprites = new FSprite[ForkSegments];

				Color glowColor = new Color(0.4f, 0.75f, 1f);

				int idx = 0;
				for (int i = 0; i < MainSegments; i++)
				{
					glowSprites[i] = MakeSprite(glowColor, 0.5f);
					glowSprites[i].shader = rCam.room.game.rainWorld.Shaders["FlatLight"];
					coreSprites[i] = MakeSprite(Color.white, 1f);
					sLeaser.sprites[idx++] = glowSprites[i];
					sLeaser.sprites[idx++] = coreSprites[i];
				}
				for (int i = 0; i < ForkSegments; i++)
				{
					forkGlowSprites[i] = MakeSprite(glowColor, 0.5f);
					forkCoreSprites[i] = MakeSprite(Color.white, 1f);
					sLeaser.sprites[idx++] = forkGlowSprites[i];
					sLeaser.sprites[idx++] = forkCoreSprites[i];
				}

				AddToContainer(sLeaser, rCam, null);
			}

			private static FSprite MakeSprite(Color color, float alpha)
			{
				return new FSprite("Futile_White")
				{
					anchorX = 0f,     // 从起点向终点延伸
					anchorY = 0.5f,   // 绕中心旋转
					color = color,
					alpha = alpha,
					// 如果运行时报错，把下面这行注释掉即可
					// shader = rCam.game.rainWorld.Shaders["Additive"],
				};
			}

			public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
			{
				base.DrawSprites(sLeaser, rCam, timeStacker, camPos);

				int pointCount = MainSegments + 1;

				// 端点失效 → 全部隐藏
				if (!start.TryGetTarget(out _) || !target.TryGetTarget(out _))
				{
					HideAll(sLeaser);
					return;
				}

				// 端点位置插值
				Vector2 interpStart = Vector2.Lerp(lastStartPos, startPos, timeStacker);
				Vector2 interpTarget = Vector2.Lerp(lastTargetPos, targetPos, timeStacker);

				// 往体表推出去，看起来是从身体表面放电
				Vector2 baseDir = interpTarget - interpStart;
				if (baseDir.sqrMagnitude < 0.0001f)
				{
					HideAll(sLeaser);
					return;
				}
				interpStart += (interpStart - interpTarget).normalized * StartRadius;
				interpTarget += (interpTarget - interpStart).normalized * StartRadius;

				Vector2 dir = interpTarget - interpStart;
				float len = dir.magnitude;
				if (len < 0.01f)
				{
					HideAll(sLeaser);
					return;
				}
				Vector2 dirNorm = dir / len;
				Vector2 perp = new Vector2(-dirNorm.y, dirNorm.x);

				// 生命周期 → alpha
				float interpLife = Mathf.Lerp(lastLife, life, timeStacker);
				float fadeAlpha = Mathf.Clamp01(1f - interpLife);
				float flicker = 0.7f + (Mathf.PerlinNoise(Time.time * 45f, flickerSeed) * 0.6f);
				float alpha = Mathf.Clamp01(fadeAlpha * flicker);

				// 颜色：白 → 淡蓝
				Color coreColor = Color.Lerp(Color.white, new Color(0.6f, 0.8f, 1f), interpLife);

				// ===== 计算主链节点 =====
				Vector2[] pts = new Vector2[pointCount];
				for (int i = 0; i < pointCount; i++)
				{
					float t = i / (float)(pointCount - 1);
					Vector2 straight = Vector2.Lerp(interpStart, interpTarget, t);
					pts[i] = straight + (perp * offsets[i]);
				}

				// ===== 画主链（两端细、中间粗）=====
				for (int i = 0; i < MainSegments; i++)
				{
					float tt = i / (float)(MainSegments - 1);
					float taper = 0.55f + (0.7f * Mathf.Sin(tt * Mathf.PI)); // 0.55 → 1.25 → 0.55
					DrawSegment(glowSprites[i], coreSprites[i], pts[i], pts[i + 1],
						BaseThickness * taper, GlowThickness * taper,
						alpha, coreColor, camPos);
				}

				// ===== 画分叉（从根部到尖端逐渐变细）=====
				if (hasFork)
				{
					Vector2 forkRoot = pts[forkParent];
					Vector2 segDir = (pts[forkParent + 1] - forkRoot).normalized;
					Vector2 forkDir = (segDir + (perp * forkDirBias)).normalized;
					float forkLen = len * ForkLengthRatio;

					Vector2[] fpts = new Vector2[ForkSegments + 1];
					for (int i = 0; i <= ForkSegments; i++)
					{
						float t = i / (float)ForkSegments;
						Vector2 straight = forkRoot + (forkDir * (forkLen * t));
						fpts[i] = straight + (perp * forkOffsets[i]);
					}

					for (int i = 0; i < ForkSegments; i++)
					{
						float tt = i / (float)(ForkSegments - 1);
						float taper = Mathf.Lerp(0.85f, 0.15f, tt);
						DrawSegment(forkGlowSprites[i], forkCoreSprites[i], fpts[i], fpts[i + 1],
							BaseThickness * taper, GlowThickness * taper,
							alpha * 0.85f, coreColor, camPos);
					}
				}
				else
				{
					for (int i = 0; i < ForkSegments; i++)
					{
						forkGlowSprites[i].isVisible = false;
						forkCoreSprites[i].isVisible = false;
					}
				}
			}

			private static void HideAll(RoomCamera.SpriteLeaser sLeaser)
			{
				for (int i = 0; i < sLeaser.sprites.Length; i++)
					sLeaser.sprites[i].isVisible = false;
			}

			private static void DrawSegment(
				FSprite glow, FSprite core,
				Vector2 a, Vector2 b,
				float coreWidth, float glowWidth,
				float alpha, Color coreColor,
				Vector2 camPos)
			{
				Vector2 seg = b - a;
				float segLen = seg.magnitude;
				if (segLen < 0.01f)
				{
					glow.isVisible = false;
					core.isVisible = false;
					return;
				}

				glow.isVisible = true;
				core.isVisible = true;

				//float angle = Custom.VecToDeg(seg / segLen) - 90f;
				float angle = Mathf.Atan2(-seg.y, seg.x) * Mathf.Rad2Deg;

				// 光晕：宽、半透明
				glow.x = a.x - camPos.x;
				glow.y = a.y - camPos.y;
				glow.scaleX = segLen / 16f;
				glow.scaleY = glowWidth / 16f;
				glow.rotation = angle;
				glow.alpha = alpha * 0.55f;

				// 核心：细、亮
				core.x = a.x - camPos.x;
				core.y = a.y - camPos.y;
				core.scaleX = segLen / 16f;
				core.scaleY = coreWidth / 16f;
				core.rotation = angle;
				core.alpha = alpha;
				core.color = coreColor;
			}

			public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer? newContatiner)
			{
				if (newContatiner == null)
					newContatiner = rCam.ReturnFContainer("Foreground");

				foreach (FSprite fsprite in sLeaser.sprites)
				{
					fsprite.RemoveFromContainer();
					newContatiner.AddChild(fsprite);
				}
			}
		}

		public class ElectricArcCosmetic : LightningBolt
		{
			private readonly Creature owner;
			private readonly float maxRadius;
			private readonly int ownerChunk;      // 0=躯干 1=臀部，可对两头放电
			private float charge;                 // 手动控制的寿命 1 → 0

			public ElectricArcCosmetic(Creature owner, float radius = 35f, float lifeSeconds = 0.2f,
				float width = 0.05f, float hue = 0.6f, int chunk = 0)
				: base(owner.bodyChunks[chunk].pos,
					   owner.bodyChunks[chunk].pos + (Custom.RNV() * radius),
					   1,                        // type 1：不衰减，寿命自己管
					   width,
					   lifeSeconds,
					   0.5f,                     // lightningParam：抖动程度
					   hue,                      // lightningType：HSL 色相（0.6 ≈ 电蓝）
					   true)                     // light：带辉光
			{
				this.owner = owner;
				this.maxRadius = radius;
				this.ownerChunk = chunk;
				this.charge = 1f;
				this.life = 1f;                  // type 1 构造里设为 1，保持住
			}

			public override void Update(bool eu)
			{
				base.Update(eu);                 // 注意：type 1 时原版不衰减 life

				if (owner == null || owner.slatedForDeletetion)
				{
					this.Destroy();
					return;
				}

				// 电弧锚在玩家身上，终点在周围球面上乱跳
				Vector2 anchor = owner.bodyChunks[ownerChunk].pos;
				this.from = anchor;
				this.target = anchor + (Custom.RNV() * maxRadius * Random.Range(0.4f, 1f));

				// 每 2 帧换一次 randomOffset → shader 里的锯齿形状刷新，滋滋抖动
				if (room.game.clock % 2 == 0)
				{
					this.randomOffset = Random.value;
				}

				// 手动衰减，先慢后快（平方衰减更像电火花熄灭）
				this.charge -= 1f / this.lifeTime;
				this.life = this.charge * this.charge;
				this.intensity = 1f;

				if (this.charge <= 0f)
				{
					this.Destroy();
				}
			}
		}

	}
}
