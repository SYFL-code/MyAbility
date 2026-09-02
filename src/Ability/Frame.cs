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
using static PhysicalObject;

namespace MySlugcat.Ability
{
	// 嫁祸能力
	public static class FrameAbility
	{
		public static void Hook()
		{
			// Player.Die
			HookManager.Register("On.Player.Die += Player_Die (FrameAbility)", new HookManager.HookData
			{
				Priority = 0,
				InitializeHooks = () => On.Player.Die += Player_Die,
				UnInitializeHooks = () => On.Player.Die -= Player_Die,
			});

			// Player.Destroy
			HookManager.Register("On.Player.Destroy += Player_Destroy (FrameAbility)", new HookManager.HookData
			{
				Priority = 0,
				InitializeHooks = () => On.Player.Destroy += Player_Destroy,
				UnInitializeHooks = () => On.Player.Destroy -= Player_Destroy,
			});

			// Lizard.Bite
			HookManager.Register("On.Lizard.Bite += Lizard_Bite (FrameAbility)", new HookManager.HookData
			{
				Priority = 0,
				InitializeHooks = () => On.Lizard.Bite += Lizard_Bite,
				UnInitializeHooks = () => On.Lizard.Bite -= Lizard_Bite,
			});

			// Vulture.Carry
			HookManager.Register("On.Vulture.Carry += Vulture_Carry (FrameAbility)", new HookManager.HookData
			{
				Priority = 0,
				InitializeHooks = () => On.Vulture.Carry += Vulture_Carry,
				UnInitializeHooks = () => On.Vulture.Carry -= Vulture_Carry,
			});

			// Creature_Violence
			HookManager.Register(
				Priority: 0,
				InitializeHooks: () => On.Creature.Violence += Creature_Violence,
				UnInitializeHooks: () => On.Creature.Violence -= Creature_Violence
			);


			// Weapon.HitSomething
			HookManager.Register("On.Weapon.HitSomething += Frame_HitSomething (FrameAbility)", new HookManager.HookData
			{
				Priority = 0,
				InitializeHooks = () => On.Weapon.HitSomething += Frame_HitSomething,
				UnInitializeHooks = () => On.Weapon.HitSomething -= Frame_HitSomething,
			});
			HookManager.Register("On.Spear.HitSomething += Frame_HitSomething (FrameAbility)", new HookManager.HookData
			{
				Priority = 0,
				InitializeHooks = () => On.Spear.HitSomething += Frame_HitSomething,
				UnInitializeHooks = () => On.Spear.HitSomething -= Frame_HitSomething,
			});
			HookManager.Register("On.Rock.HitSomething += Frame_HitSomething (FrameAbility)", new HookManager.HookData
			{
				Priority = 0,
				InitializeHooks = () => On.Rock.HitSomething += Frame_HitSomething,
				UnInitializeHooks = () => On.Rock.HitSomething -= Frame_HitSomething,
			});
			HookManager.Register("On.ScavengerBomb.HitSomething += Frame_HitSomething (FrameAbility)", new HookManager.HookData
			{
				Priority = 0,
				InitializeHooks = () => On.ScavengerBomb.HitSomething += Frame_HitSomething,
				UnInitializeHooks = () => On.ScavengerBomb.HitSomething -= Frame_HitSomething,
			});
			if (ModManager.MSC)
			{
				HookManager.Register("On.MoreSlugcats.LillyPuck.HitSomething += Frame_HitSomething (FrameAbility)", new HookManager.HookData
				{
					Priority = 0,
					InitializeHooks = () => On.MoreSlugcats.LillyPuck.HitSomething += Frame_HitSomething,
					UnInitializeHooks = () => On.MoreSlugcats.LillyPuck.HitSomething -= Frame_HitSomething,
				});
			}
			if (ModManager.Watcher)
			{
				HookManager.Register("On.Boomerang.HitSomething += Frame_HitSomething (FrameAbility)", new HookManager.HookData
				{
					Priority = 0,
					InitializeHooks = () => On.Boomerang.HitSomething += Frame_HitSomething,
					UnInitializeHooks = () => On.Boomerang.HitSomething -= Frame_HitSomething,
				});
			}
		}

		public static bool Frame(Creature creature, Creature? target)
		{
			if (!creature.dead)
			{
				if (target != null)
				{
					FramePos(creature, target);

					creature.dead = false;
					creature.stun = 0;

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
						bool FrameResult = Frame(player, target);
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
			return PenetrationAbility.orig_HitSomething(orig_, weapon, result, eu);
		}


		private static void Creature_Violence(On.Creature.orig_Violence orig, Creature creature, BodyChunk source, Vector2? directionAndMomentum,
			BodyChunk hitChunk, PhysicalObject.Appendage.Pos hitAppendage, Creature.DamageType type, float damage, float stunBonus)
		{
			if (hitChunk != null && hitChunk.owner is Player player)
			{
				if (player.GetModule().FrameAbility)
				{
					if (type == Creature.DamageType.Bite ||
						type == Creature.DamageType.Electric ||
						type == Creature.DamageType.Stab)
					{
						if (creature is Lizard)
						{
							Creature? target = Helper.FindNearestCreature(player.mainBodyChunk.pos, player.room, [player, creature]);

							bool FrameResult = Frame(player, target);
							if (FrameResult)
							{
								orig.Invoke(target, source, directionAndMomentum, hitChunk, hitAppendage, type, damage, stunBonus);
							}
						}

						//偷渡虫情况特殊处理
						if (source != null && source.owner is StowawayBug)
						{
							//钩子伤害不处理
							if (damage < 1f)
								orig.Invoke(creature, source, directionAndMomentum, hitChunk, hitAppendage, type, damage, stunBonus);
							else
							{
								orig.Invoke(creature, source, directionAndMomentum, hitChunk, hitAppendage, type, 0, stunBonus);
								player.stun = 0;
							}
							return;
						}

						//防止玩家被咬死
						orig.Invoke(creature, source, directionAndMomentum, hitChunk, hitAppendage, type, 0, stunBonus);
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
						bool FrameResult = Frame(player, target);
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
						bool FrameResult = Frame(player, target);
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
					bool FrameResult = Frame(player, target);
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
					bool FrameResult = Frame(player, target);
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
