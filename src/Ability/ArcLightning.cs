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
				if (isElectricCreature)
				{
					Recharge(weapon, start, player);
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
				room.AddObject(new DebugLine(start, target, stunBonus));
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

	}
}
