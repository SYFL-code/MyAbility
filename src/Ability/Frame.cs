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

					target.Violence(creature.mainBodyChunk, null, target.mainBodyChunk, null, Creature.DamageType.None, 0.1f, 60f);

					return true;
				}
			}
			return false;
		}

		public static void FramePos(Creature creature, Creature target)
		{
			Vector2 creaturePos = new Vector2(creature.mainBodyChunk.pos.x, creature.mainBodyChunk.pos.y);
			Vector2 targetPos = new Vector2(target.mainBodyChunk.pos.x, target.mainBodyChunk.pos.y);

			creature.room.AddObject(new ExplosionSpikes(creature.room, creature.mainBodyChunk.pos, 14, 30f, 9f, 7f, 170f, creature.ShortCutColor()));
			creature.room.AddObject(new ShockWave(creature.mainBodyChunk.pos, 500f, 0.080f, 10, false));

			target.room.AddObject(new ExplosionSpikes(target.room, target.mainBodyChunk.pos, 14, 30f, 9f, 7f, 170f, target.ShortCutColor()));
			target.room.AddObject(new ShockWave(target.mainBodyChunk.pos, 500f, 0.080f, 10, false));

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

			if (result.obj is Player player)
			{
				if (player.GetModule().FrameAbility)
				{
					Creature? target = Helper.FindNearestCreature(player.mainBodyChunk.pos, player.room, [player]);

					if (target != null)
					{
						bool FrameResult = FrameTarget(player, target);
						if (FrameResult)
						{
							result.obj = target;
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
			if (creature is Player player)
			{
				if (player.GetModule().FrameAbility)
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
								Creature? target = Helper.FindNearestCreature(player.mainBodyChunk.pos, player.room, [player, stowawayBug]);

								bool FrameResult = FrameTarget(player, target);
								if (FrameResult)
								{
									orig.Invoke(target, source, directionAndMomentum, target?.mainBodyChunk, null, type, damage, stunBonus);
									player.stun = 0;

									return;
								}
							}
						}

						if (killer != null)
						{
							Creature? target = Helper.FindNearestCreature(player.mainBodyChunk.pos, player.room, [player, killer]);

							bool FrameResult = FrameTarget(player, target);
							if (FrameResult)
							{
								orig.Invoke(target, source, directionAndMomentum, target?.mainBodyChunk, null, type, damage, stunBonus);
								player.stun = 0;

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
			if (chunk?.owner is Player player)
			{
				if (player.GetModule().FrameAbility)
				{
					Creature? target = Helper.FindNearestCreature(player.mainBodyChunk.pos, player.room, [player, lizard]);

					if (target != null)
					{
						bool FrameResult = FrameTarget(player, target);
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
			if (vulture.grasps?[0]?.grabbed is Player player)
			{
				if (player.GetModule().FrameAbility)
				{
					Creature? target = Helper.FindNearestCreature(player.mainBodyChunk.pos, player.room, [player, vulture]);

					if (target != null)
					{
						bool FrameResult = FrameTarget(player, target);
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
			if (player.GetModule().FrameAbility)
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
			if (player.GetModule().FrameAbility)
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


	}
}
