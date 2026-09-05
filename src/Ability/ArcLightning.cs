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
			if (weapon.thrownBy is Creature)
			{
				weaponModule.Owner = new(weapon.thrownBy);
			}
			if (weaponModule.Owner.TryGetTarget(out var target) && target is Player player)
			{
				if (player.GetModule().ArcLightningAbility)
				{
					if (result.obj is Creature hitCreature)
					{
						weaponModule.stuckInObject.TryGetTarget(out var stuckInObject);
						if (stuckInObject != hitCreature || weaponModule.stuckInObjectTime > 30)
						{
							List<Creature> exclude = [player];
							// 执行连锁
							ArcTriggerChain(weapon, hitCreature, player, weapon.firstChunk.vel.normalized, ref exclude, 5);
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

			Vector2 startPos = start.mainBodyChunk.pos;
			direction = direction.normalized;

			List<Creature> candidates = Helper.FindCreaturesInCone(startPos, direction, room,
				ConeHalfAngle, ChainRadius, exclude, null, false);

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
				if (isElectricCreature)
				{
					Recharge(weapon, start, player);
					remainingChains += 3;
				}
				if (target is not BigEel && !isElectricCreature)
				{
					// 施加电击伤害和眩晕
					target.Violence(player.firstChunk,
						new Vector2?(Custom.DirVec(start.firstChunk.pos, target.firstChunk.pos) * 5f),
						target.firstChunk,
						null,
						Creature.DamageType.Electric,
						0.1f,
						(target is not Player) ? (320f * Mathf.Lerp(target.Template.baseStunResistance, 1f, 0.5f)) : 140f);

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
				room.AddObject(new DebugSprite(start, target, (target is not Player) ? (320f * Mathf.Lerp(target.Template.baseStunResistance, 1f, 0.5f)) : 140f));
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

		public static void Recharge(Weapon weapon, Creature start, Player player)
		{
			Room room = start.room;

			room.PlaySound(SoundID.Jelly_Fish_Tentacle_Stun, start.firstChunk);
			room.AddObject(new Explosion.ExplosionLight(start.firstChunk.pos, 200f, 1f, 4, new Color(0.7f, 1f, 1f)));
			//this.Spark();
			//this.Zap();
			//room.AddObject(new ZapCoil.ZapFlash(this.sparkPoint, 25f));
		}

		public static bool CheckElectricCreature(Creature otherObject)
		{
			return otherObject is Centipede || otherObject is BigJellyFish || otherObject is Inspector;
		}

		public class DebugSprite : CosmeticSprite
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

			public DebugSprite(Creature start, Creature target, float lifeTime)
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
			}

			public override void Update(bool eu)
			{
				base.Update(eu);

				this.lastLife = this.life;
				this.life += 1f / (float)this.lifeTime;

				if (this.lastLife > 1f)
				{
					this.Destroy();
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
			}

			public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
			{
				sLeaser.sprites = new FSprite[1];
				sLeaser.sprites[0] = new FSprite("Futile_White")
				{
					anchorX = 0f,      // 锚点设为0，使scaleX从起点向终点延伸
					anchorY = 0.5f,    // Y轴居中，旋转中心
					scaleX = Vector2.Distance(targetPos, startPos) / 4f,
					scaleY = 1f / 4f, // 设置线条宽度
					alpha = 1f,
				};


				this.AddToContainer(sLeaser, rCam, null);
			}

			public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
			{
				base.DrawSprites(sLeaser, rCam, timeStacker, camPos);

				// 任一生物失效则隐藏线条
				if (!start.TryGetTarget(out _) || !target.TryGetTarget(out _))
				{
					sLeaser.sprites[0].isVisible = false;
					return;
				}

				sLeaser.sprites[0].isVisible = true;

				// 对起点和终点都进行插值，消除抖动
				Vector2 interpolatedStart = Vector2.Lerp(lastStartPos, startPos, timeStacker);
				Vector2 interpolatedTarget = Vector2.Lerp(lastTargetPos, targetPos, timeStacker);

				float distance = Vector2.Distance(interpolatedTarget, interpolatedStart);

				Vector2 direction = (interpolatedTarget - interpolatedStart).normalized;
				float targetAngle = Custom.VecToDeg(direction);
				//float rotation = Custom.AimFromOneVectorToAnother(interpolatedStart, interpolatedTarget);

				float life = Mathf.Lerp(this.lastLife, this.life, timeStacker);
				
				sLeaser.sprites[0].x = interpolatedStart.x - camPos.x;
				sLeaser.sprites[0].y = interpolatedStart.y - camPos.y;
				sLeaser.sprites[0].scaleX = distance / 16f;
				if (distance > 0.01f)
				{
					sLeaser.sprites[0].rotation = targetAngle - 90f;
				}
				sLeaser.sprites[0].alpha = 1f - life;
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
