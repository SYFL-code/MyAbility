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
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Unity.Microsoft.GDK;
using UnityEngine;
using UnityEngine.UIElements;
using Watcher;
using static Menu.Remix.InternalOI;
using static MonoMod.InlineRT.MonoModRule;
using static PhysicalObject;

namespace MySlugcat.Ability
{
	// 嫁祸能力
	public static class Frame
	{
		public static bool FrameTarget(Creature creature, Creature? target)
		{
			if (!creature.dead)
			{
				if (target != null)
				{
					FramePos(creature, target);

					creature.dead = false;
					creature.stun = 0;

					if (creature is Player player)
					{
						ExitGameOverMode(player);

						if (!Plugin.DebugMode || !Debugger.bools[0, false])
						{
							if (player.GetModule().CamouflageAbility)
							{
								player.GetCamouflageModule(out var camouflageModule);
								camouflageModule.CdTimer = 200;
							}
						}
					}

					target.Violence(creature.mainBodyChunk, null, target.mainBodyChunk, null, Creature.DamageType.None, 0.01f, 0f);

					return true;
				}
			}
			return false;
		}

		public static void FramePos(Creature creature, Creature target)
		{
			Vector2 creaturePos = new Vector2(creature.mainBodyChunk.pos.x, creature.mainBodyChunk.pos.y);
			Vector2 targetPos = new Vector2(target.mainBodyChunk.pos.x, target.mainBodyChunk.pos.y);

			try
			{
				Vector2 a = new Vector2(creature.mainBodyChunk.pos.x, creature.mainBodyChunk.pos.y);
				Vector2 b = new Vector2(target.mainBodyChunk.pos.x, target.mainBodyChunk.pos.y);

				bool sameRoom = creature.room == target.room && creature.room != null;

				if (sameRoom)
				{
					creature.room?.AddObject(new FrameBlackMireFX(creature.room, a, b, creature.ShortCutColor(), false, false));
					target.room.AddObject(new FrameBlackMireFX(target.room, b, b, target.ShortCutColor(), true, false));
				}
				else
				{
					if (creature.room != null)
						creature.room.AddObject(new FrameBlackMireFX(creature.room, a, a, creature.ShortCutColor(), true, false));

					if (target.room != null)
						target.room.AddObject(new FrameBlackMireFX(target.room, b, b, target.ShortCutColor(), true, false));
				}

				//creature.room.AddObject(new ExplosionSpikes(creature.room, creature.mainBodyChunk.pos, 14, 30f, 9f, 7f, 170f, creature.ShortCutColor()));
				//creature.room.AddObject(new ShockWave(creature.mainBodyChunk.pos, 500f, 0.080f, 10, false));

				//target.room.AddObject(new ExplosionSpikes(target.room, target.mainBodyChunk.pos, 14, 30f, 9f, 7f, 170f, target.ShortCutColor()));
				//target.room.AddObject(new ShockWave(target.mainBodyChunk.pos, 500f, 0.080f, 10, false));
			}
			catch (Exception e)
			{
				Log.LogError($"creature.room != null {creature.room != null}, creature.mainBodyChunk.pos != null {creature.mainBodyChunk.pos != null}");
				Log.LogError($"creature.ShortCutColor() != null {creature.ShortCutColor() != null}");

				Log.LogError($"target.room != null {target.room != null}, target.mainBodyChunk.pos != null {target.mainBodyChunk.pos != null}");
				Log.LogError($"target.ShortCutColor() != null {target.ShortCutColor() != null}");

				Log.LogError(e);
			}

			Teleport(creature, targetPos);
			Teleport(target, creaturePos);
		}

		public static void Teleport(Creature creature, Vector2 targetPos)
		{
			if (creature is Player player)
			{
				player.SuperHardSetPosition(targetPos);

				player.feetStuckPos = null;
			}
			else
			{
				Vector2 firstChunkOldPos = creature.firstChunk.pos;
				List<Vector2> offset = [];

				for (int i = 0; i < creature.bodyChunks.Length; i++)
				{
					offset.Add(creature.bodyChunks[i].pos - firstChunkOldPos);
				}

				Helper.SetObjectPosition(creature, targetPos);

				for (int i = 0; i < creature.bodyChunks.Length; i++)
				{
					creature.bodyChunks[i].pos += offset[i];
					creature.bodyChunks[i].vel = Vector2.zero;
				}
			}
		}

		public static void ExitGameOverMode(Player player)
		{
			// 当复活玩家时尝试退出 "游戏结束模式" 。可能与其他一些模块不兼容。
			if (!player.isNPC)
			{
				for (int i = 0; i < (player.room?.game?.cameras?.Length ?? 0); i++)
				{
					if (player.room?.game?.cameras[i]?.hud?.textPrompt != null)
					{
						player.room.game.cameras[i].hud.textPrompt.gameOverMode = false;
					}
				}

				if (player.room?.game?.arenaOverlay != null)
				{
					player.room.game.arenaOverlay.ShutDownProcess();

					ProcessManager manager = player.room.game.manager;
					if (manager != null)
					{
						List<MainLoopProcess> sideProcesses = manager.sideProcesses;
						if (sideProcesses != null)
						{
							sideProcesses.Remove(player.room.game.arenaOverlay);
						}
					}
					player.room.game.arenaOverlay = null;
					if (player.room.game.session is ArenaGameSession arenaSession)
					{
						arenaSession.sessionEnded = false;
						arenaSession.challengeCompleted = false;
						arenaSession.endSessionCounter = -1;
					}
				}
			}
		}

		public static bool Frame_HitSomething<O, W>(O orig_, W weapon, SharedPhysics.CollisionResult result, bool eu)
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


			if (result.obj is Creature hitCreature)
			{
				hitCreature.GetModule(out var module);
				if (module.FrameAbility)
				{
					if (!module.StalwartShellAbility || !hitCreature.GetStalwartShellModule().validity)
					{
						Creature? target = Helper.FindNearestCreature(hitCreature.mainBodyChunk.pos, hitCreature.room,
							[hitCreature], [hitCreature.GetType(), typeof(Fly)]);

						if (target != null)
						{
							bool FrameResult = FrameTarget(hitCreature, target);
							if (FrameResult)
							{
								result.obj = target;
								result.chunk = target.mainBodyChunk;
								result.onAppendagePos = null;
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


		public static void Creature_Violence(On.Creature.orig_Violence orig, Creature creature, BodyChunk? source, Vector2? directionAndMomentum,
			BodyChunk hitChunk, PhysicalObject.Appendage.Pos hitAppendage, Creature.DamageType type, float damage, float stunBonus)
		{
			if (creature is Creature hitCreature)
			{
				if (hitCreature.GetModule().FrameAbility)
				{
					if (type == Creature.DamageType.Bite ||
						type == Creature.DamageType.Electric ||
						type == Creature.DamageType.Stab)
					{
						Creature? killer = null;
						if (source?.owner is Creature c)
						{
							killer = c;
						}
						if (source?.owner is Weapon w)
						{
							killer = w.thrownBy;
						}


						//if (killer is Lizard)
						//{
						//	Creature? target = Helper.FindNearestCreature(player.mainBodyChunk.pos, player.room, [player, killer]);

						//	bool FrameResult = FrameTarget(player, target);
						//	if (FrameResult)
						//	{
						//		orig.Invoke(target, source, directionAndMomentum, target?.mainBodyChunk, null, type, damage, stunBonus);
						//		return;
						//	}
						//}

						//偷渡虫情况特殊处理
						if (source != null && source.owner is StowawayBug stowawayBug)
						{
							//钩子伤害不处理
							if (damage < 1f)
							{ }
							else
							{
								Creature? target = Helper.FindNearestCreature(hitCreature.mainBodyChunk.pos, hitCreature.room,
									[hitCreature, stowawayBug], [hitCreature.GetType(), typeof(Fly)]);

								bool FrameResult = FrameTarget(hitCreature, target);
								if (FrameResult)
								{
									orig.Invoke(target, source, directionAndMomentum, target?.mainBodyChunk, null, type, damage, stunBonus);
									hitCreature.stun = 0;

									return;
								}
							}
						}

						if (killer != null)
						{
							Creature? target = Helper.FindNearestCreature(hitCreature.mainBodyChunk.pos, hitCreature.room,
								[hitCreature, killer], [hitCreature.GetType(), typeof(Fly)]);

							bool FrameResult = FrameTarget(hitCreature, target);
							if (FrameResult)
							{
								orig.Invoke(target, source, directionAndMomentum, target?.mainBodyChunk, null, type, damage, stunBonus);
								hitCreature.stun = 0;

								return;
							}
						}

						//防止玩家被咬死
						//orig.Invoke(creature, source, directionAndMomentum, hitChunk, hitAppendage, type, 0, stunBonus);
					}
				}
			}

			orig.Invoke(creature, source, directionAndMomentum, hitChunk, hitAppendage, type, damage, stunBonus);
		}

		public static void Lizard_Bite(On.Lizard.orig_Bite orig, Lizard lizard, BodyChunk chunk)
		{
			if (chunk?.owner is Creature hitCreature)
			{
				if (hitCreature.GetModule().FrameAbility)
				{
					Creature? target = Helper.FindNearestCreature(hitCreature.mainBodyChunk.pos, hitCreature.room,
						[hitCreature, lizard], [hitCreature.GetType(), typeof(Fly)]);

					if (target != null)
					{
						bool FrameResult = FrameTarget(hitCreature, target);
						if (FrameResult)
						{
							chunk = target.mainBodyChunk;
						}
					}
				}
			}

			orig.Invoke(lizard, chunk);
		}

		public static void Vulture_Carry(On.Vulture.orig_Carry orig, Vulture vulture)
		{
			if (vulture.grasps?[0]?.grabbed is Creature hitCreature)
			{
				if (hitCreature.GetModule().FrameAbility)
				{
					Creature? target = Helper.FindNearestCreature(hitCreature.mainBodyChunk.pos, hitCreature.room,
						[hitCreature, vulture], [hitCreature.GetType(), typeof(Fly)]);

					if (target != null)
					{
						bool FrameResult = FrameTarget(hitCreature, target);
						if (FrameResult)
						{
							vulture.grasps[0].grabbed = target;
						}
					}
				}
			}

			orig.Invoke(vulture);
		}


		public static void Player_Die(On.Player.orig_Die orig, Player player)
		{
			Creature hitCreature = player;
			if (hitCreature.GetModule().FrameAbility)
			{
				Creature? target = Helper.FindNearestCreature(player.mainBodyChunk.pos, player.room, [player]);

				if (target != null)
				{
					bool FrameResult = FrameTarget(player, target);
					if (FrameResult)
					{
						return;
					}
				}
			}

			orig(player);
		}

		public static void Player_Destroy(On.Player.orig_Destroy orig, Player player)
		{
			Creature hitCreature = player;
			if (hitCreature.GetModule().FrameAbility)
			{
				Creature? target = Helper.FindNearestCreature(player.mainBodyChunk.pos, player.room, [player]);

				if (target != null)
				{
					bool FrameResult = FrameTarget(player, target);
					if (FrameResult)
					{
						target.Destroy();

						return;
					}
				}
			}

			orig(player);
		}


		public class FrameBlackMireFX : CosmeticSprite
		{
			private const int NormalLife = 34;
			private const int HeavyLife = 54;

			private readonly Vector2 fromPos;
			private readonly Vector2 toPos;
			private readonly Color color;
			private readonly bool targetOnly;
			private readonly bool heavy;

			private readonly int life;
			private int age;
			private float lastT;
			private float t;

			private readonly System.Random rnd;

			private const int FogCount = 14;
			private const int TargetCount = 9;
			private const int LineSegments = 11;

			private readonly Vector2[] fogPos = new Vector2[FogCount];
			private readonly Vector2[] fogLast = new Vector2[FogCount];
			private readonly Vector2[] fogVel = new Vector2[FogCount];
			private readonly float[] fogSize = new float[FogCount];
			private readonly float[] fogDelay = new float[FogCount];

			private readonly Vector2[] tarPos = new Vector2[TargetCount];
			private readonly Vector2[] tarLast = new Vector2[TargetCount];
			private readonly Vector2[] tarVel = new Vector2[TargetCount];
			private readonly float[] tarSize = new float[TargetCount];

			private readonly float[] lineJitter = new float[LineSegments];
			private readonly float[] linePhase = new float[LineSegments];
			private readonly float[] lineThick = new float[LineSegments];

			public FrameBlackMireFX(Room room, Vector2 fromPos, Vector2 toPos, Color color, bool targetOnly, bool heavy)
			{
				this.room = room;
				this.pos = fromPos;
				this.lastPos = fromPos;
				this.vel = Vector2.zero;

				this.fromPos = fromPos;
				this.toPos = toPos;
				this.color = color;
				this.targetOnly = targetOnly;
				this.heavy = heavy;
				this.life = heavy ? HeavyLife : NormalLife;

				this.rnd = new System.Random(
					(int)(fromPos.x * 13.37f) ^
					(int)(fromPos.y * 71.13f) ^
					(int)(toPos.x * 3.19f) ^
					(int)(toPos.y * 9.73f) ^
					(heavy ? 999983 : 31337)
				);

				Vector2 dir = toPos - fromPos;
				if (dir.magnitude < 1f) dir = new Vector2(1f, 0f);
				dir.Normalize();

				for (int i = 0; i < FogCount; i++)
				{
					float a = (float)(rnd.NextDouble() * Math.PI * 2.0);
					float r = (float)((rnd.NextDouble() * 26.0) + 5.0);
					Vector2 off = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;

					fogPos[i] = fromPos + off;
					fogLast[i] = fogPos[i];
					fogVel[i] = (dir * (float)((rnd.NextDouble() * 2.0) + 0.8f)) + (off * 0.015f);
					fogSize[i] = (float)((rnd.NextDouble() * 0.9) + 0.55);
					fogDelay[i] = (float)(rnd.NextDouble() * 4.0);
				}

				for (int i = 0; i < TargetCount; i++)
				{
					float a = (float)(rnd.NextDouble() * Math.PI * 2.0);
					float r = (float)((rnd.NextDouble() * 18.0) + 4.0);
					Vector2 off = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;

					tarPos[i] = toPos + off;
					tarLast[i] = tarPos[i];
					tarVel[i] = (off.normalized * (float)((rnd.NextDouble() * 1.7) + 0.55f)) - (dir * 0.7f);
					tarSize[i] = (float)((rnd.NextDouble() * 0.7) + 0.35);
				}

				for (int i = 0; i < LineSegments; i++)
				{
					lineJitter[i] = (float)((rnd.NextDouble() * 2.0) - 1.0);
					linePhase[i] = (float)(rnd.NextDouble() * Math.PI * 2.0);
					lineThick[i] = (float)((rnd.NextDouble() * 0.5) + 0.5);
				}
			}

			public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
			{
				int count = FogCount + TargetCount + 1;
				if (!targetOnly) count += LineSegments;

				sLeaser.sprites = new FSprite[count];

				int i = 0;

				for (int f = 0; f < FogCount; f++, i++)
				{
					sLeaser.sprites[i] = new FSprite("Futile_White")
					{
						anchorX = 0.5f,
						anchorY = 0.5f,
						color = Color.Lerp(color, Color.black, 0.82f),
						alpha = 0.0f
					};
				}

				if (!targetOnly)
				{
					for (int l = 0; l < LineSegments; l++, i++)
					{
						sLeaser.sprites[i] = new FSprite("Futile_White")
						{
							anchorX = 0.5f,
							anchorY = 0f,
							color = Color.Lerp(Color.black, color, 0.16f),
							alpha = 0.0f
						};
					}
				}

				for (int k = 0; k < TargetCount; k++, i++)
				{
					sLeaser.sprites[i] = new FSprite("Futile_White")
					{
						anchorX = 0.5f,
						anchorY = 0.5f,
						color = Color.Lerp(color, Color.black, 0.55f),
						alpha = 0.0f
					};
				}

				sLeaser.sprites[i] = new FSprite("Futile_White")
				{
					anchorX = 0.5f,
					anchorY = 0.5f,
					color = Color.Lerp(color, Color.white, heavy ? 0.55f : 0.35f),
					alpha = 0.0f
				};

				AddToContainer(sLeaser, rCam, null);
			}

			public override void Update(bool eu)
			{
				base.Update(eu);

				age++;
				lastT = t;
				t = Mathf.Clamp01((float)age / (float)life);

				Vector2 dir = toPos - fromPos;
				if (dir.magnitude < 1f) dir = new Vector2(1f, 0f);
				dir.Normalize();

				float collapse = Mathf.Clamp01(age / 6f);
				float jet = Mathf.Clamp01((age - 4) / 8f);

				for (int f = 0; f < FogCount; f++)
				{
					fogLast[f] = fogPos[f];

					Vector2 target = fromPos + (dir * (20f + (f * 2.5f)));
					fogPos[f] = Vector2.Lerp(fogPos[f], target, collapse * 0.18f);

					if (age > fogDelay[f])
					{
						fogVel[f] += dir * (0.34f * jet);
						fogVel[f] *= 0.90f;
						fogPos[f] += fogVel[f];
					}

					fogSize[f] *= 0.985f;
				}

				for (int k = 0; k < TargetCount; k++)
				{
					tarLast[k] = tarPos[k];
					tarVel[k] *= 0.88f;
					tarVel[k] += Vector2.down * 0.03f;
					tarPos[k] += tarVel[k];
					tarSize[k] *= 0.982f;
				}

				if (age >= life)
				{
					slatedForDeletetion = true;
				}
			}

			public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
			{
				base.DrawSprites(sLeaser, rCam, timeStacker, camPos);

				if (sLeaser.deleteMeNextFrame) return;

				float tt = Mathf.Lerp(lastT, t, timeStacker);

				float fogIn = Mathf.Clamp01(tt * 4.5f);
				float fogOut = Mathf.Clamp01((1f - tt) * 2.2f);
				float fogAlpha = Mathf.Min(fogIn, fogOut) * (heavy ? 0.62f : 0.48f);

				float lineIn = Mathf.Clamp01((tt - 0.08f) * 7f);
				float lineOut = Mathf.Clamp01((1f - tt) * 2.6f);
				float lineAlpha = Mathf.Min(lineIn, lineOut) * (heavy ? 0.72f : 0.55f);

				float tarIn = Mathf.Clamp01((tt - 0.12f) * 6f);
				float tarOut = Mathf.Clamp01((1f - tt) * 2.0f);
				float tarAlpha = Mathf.Min(tarIn, tarOut) * (heavy ? 0.75f : 0.58f);

				int i = 0;

				for (int f = 0; f < FogCount; f++, i++)
				{
					FSprite s = (FSprite)sLeaser.sprites[i];
					Vector2 p = Vector2.Lerp(fogLast[f], fogPos[f], timeStacker) - camPos;

					s.x = p.x;
					s.y = p.y;
					s.scale = fogSize[f] * Mathf.Lerp(1.8f, 0.7f, tt);
					s.rotation = (f * 37.7f) + (Mathf.Rad2Deg * tt * (f % 2 == 0 ? 0.8f : -0.6f));
					s.alpha = fogAlpha * (0.55f + (0.45f * Mathf.Sin((tt * Mathf.PI) + f)));
				}

				if (!targetOnly)
				{
					Vector2 a = fromPos - camPos;
					Vector2 b = toPos - camPos;
					Vector2 d = b - a;
					float len = d.magnitude;
					if (len > 1f)
					{
						Vector2 nd = d / len;
						Vector2 perp = new Vector2(-nd.y, nd.x);

						for (int l = 0; l < LineSegments; l++, i++)
						{
							FSprite s = (FSprite)sLeaser.sprites[i];

							float t0 = (float)l / LineSegments;
							float t1 = (float)(l + 1) / LineSegments;

							float wob0 = Mathf.Sin(linePhase[l] + (age * 0.31f)) * lineJitter[l] * 5.5f;
							float wob1 = Mathf.Sin(linePhase[l] + ((age + 1) * 0.31f)) * lineJitter[l] * 5.5f;

							Vector2 p0 = a + (d * t0) + (perp * wob0 * Mathf.Sin(t0 * Mathf.PI));
							Vector2 p1 = a + (d * t1) + (perp * wob1 * Mathf.Sin(t1 * Mathf.PI));

							Vector2 seg = p1 - p0;
							float sl = seg.magnitude;
							if (sl < 0.1f) sl = 0.1f;

							float thick = Mathf.Lerp(1.8f, 7.5f, Mathf.Pow(t1, 2.2f)) * lineThick[l];
							thick *= Mathf.Lerp(1f, 0.65f, tt);

							s.x = (p0.x + p1.x) * 0.5f;
							s.y = (p0.y + p1.y) * 0.5f;
							s.rotation = (Mathf.Atan2(seg.y, seg.x) * Mathf.Rad2Deg) - 90f;
							s.scaleX = thick / 16f;
							s.scaleY = sl / 16f;
							s.alpha = lineAlpha * Mathf.Lerp(0.55f, 1f, t1);
						}
					}
					else
					{
						for (int l = 0; l < LineSegments; l++, i++)
						{
							sLeaser.sprites[i].alpha = 0f;
						}
					}
				}

				for (int k = 0; k < TargetCount; k++, i++)
				{
					FSprite s = (FSprite)sLeaser.sprites[i];
					Vector2 p = Vector2.Lerp(tarLast[k], tarPos[k], timeStacker) - camPos;

					s.x = p.x;
					s.y = p.y;
					s.scale = tarSize[k] * Mathf.Lerp(0.45f, 1.35f, Mathf.Sin(tt * Mathf.PI));
					s.rotation = (k * 41.3f) - (Mathf.Rad2Deg * tt * 0.7f);
					s.alpha = tarAlpha;
				}

				FSprite core = (FSprite)sLeaser.sprites[i];
				Vector2 cp = toPos - camPos;
				float corePulse = Mathf.Clamp01((tt - 0.10f) * 8f) * Mathf.Clamp01((1f - tt) * 3.2f);

				core.x = cp.x;
				core.y = cp.y;
				core.rotation = age * 3f;
				core.scaleX = Mathf.Lerp(0.25f, heavy ? 1.0f : 0.65f, corePulse);
				core.scaleY = Mathf.Lerp(0.25f, heavy ? 1.0f : 0.65f, corePulse);
				core.alpha = corePulse * (heavy ? 0.65f : 0.38f);
			}

			public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
			{
			}

			public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer? newContatiner)
			{
				if (newContatiner == null)
				{
					newContatiner = rCam.ReturnFContainer("Water");
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
