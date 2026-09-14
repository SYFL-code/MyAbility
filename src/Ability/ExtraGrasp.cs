using BepInEx.Logging;
using CommonUtils.Core;
using HarmonyLib;
using IL;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using MoreSlugcats;
using MySlugcat.Ability;
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using UnityEngine;
using UnityEngine.XR;
using Watcher;
using static CommonUtils.Core.HookManager;
using static PhysicalObject;

namespace MySlugcat.Ability
{
	// 额外抓取
	public static class ExtraGrasp
	{
		public static int ExtraGraspsCount = 2;

		public static void Player_ctor(On.Player.orig_ctor orig, Player player, AbstractCreature abstractCreature, World world)
		{
			orig.Invoke(player, abstractCreature, world);

			player.GetModule(out var module);
			if (module.ExtraGraspAbility)
			{
				player.grasps = new Player.Grasp[player.grasps.Length + ExtraGraspsCount];
			}
		}

		public static void Creature_SwitchGrasps(On.Creature.orig_SwitchGrasps orig, Creature creature, int a, int b)
		{
			if (creature is Player player)
			{
				player.GetModule(out var module);
				if (module.ExtraGraspAbility)
				{
					// 保存最后一个
					var last = player.grasps[player.grasps.Length - 1];

					for (int i = player.grasps.Length - 1; i > 0; i--)
					{
						// 将每个元素 = 上一个元素
						if (player.grasps[i] != null || player.grasps[i - 1] != null)
						{
							player.grasps[i] = player.grasps[i - 1];
						}
					}
					// 最后一个放到索引 0
					if (player.grasps[0] != null || last != null)
					{
						player.grasps[0] = last;
					}
					player.UpdateGraspIndexes();

					return;
				}
			}

			orig(creature, a, b);
		}

		public static void GraphicsModuleUpdated(On.Player.orig_GraphicsModuleUpdated orig, Player player, bool actuallyViewed, bool eu)
		{
			orig(player, actuallyViewed, eu);

			for (int i = 0; i < player.grasps.Length; i++)
			{
				if (player.grasps[i] != null && i >= 2)
				{
					var grasp = player.grasps[i];               // 当前抓取实例
					var grabbed = grasp.grabbed;                // 被抓取的对象
					var grabbedChunk = grasp.grabbedChunk;      // 被抓取对象的物理块
					var mainChunk = player.mainBodyChunk;       // 玩家主身体块

					if (grabbed is Player grabbedPlayer && !grabbedPlayer.dead)
					{
						Vector2 direction = Custom.DirVec(mainChunk.pos, grabbedChunk.pos);
						float currentDistance = Vector2.Distance(mainChunk.pos, grabbedChunk.pos);
						float desiredDistance = 15f;

						float massRatio = grabbedChunk.mass / (mainChunk.mass + grabbedChunk.mass);

						// 如果正在进入管道（捷径）
						if (player.enteringShortCut != null)
						{
							massRatio = 0f;
						}

						// 距离超过期望值时，双方互相拉近
						if (currentDistance > desiredDistance)
						{
							Vector2 selfMove = direction * ((currentDistance - desiredDistance) * massRatio * 0.5f);
							mainChunk.pos += selfMove;
							mainChunk.vel += selfMove;

							Vector2 grabbedMove = direction * ((currentDistance - desiredDistance) * (1f - massRatio));
							grabbedChunk.pos -= grabbedMove;
							grabbedChunk.vel -= grabbedMove;
						}

						// 爬梁时的重力补偿：如果自己在爬梁，而被抓玩家不在爬梁，则给被抓玩家一个向上的速度
						if (player.bodyMode == Player.BodyModeIndex.ClimbingOnBeam &&
							player.animation != Player.AnimationIndex.BeamTip && player.animation != Player.AnimationIndex.StandOnBeam &&
							grabbedPlayer.bodyMode != Player.BodyModeIndex.ClimbingOnBeam)
						{
							grabbedChunk.vel.y += grabbed.gravity * (1f - grabbedChunk.submersion) * 0.75f;
						}

						// 如果抓取物是“拖拽”类型，且距离过远，则强制释放
						if (player.Grabability(grabbed) == Player.ObjectGrabability.Drag && currentDistance > (desiredDistance * 2f) + 30f)
						{
							player.ReleaseGrasp(i);
						}
					}
					else if (player.HeavyCarry(grabbed))
					{
						Vector2 direction = Custom.DirVec(mainChunk.pos, grabbedChunk.pos);
						float currentDistance = Vector2.Distance(mainChunk.pos, grabbedChunk.pos);
						float desiredDistance = 5f + grabbedChunk.rad;

						if (grabbed is Cicada)
						{
							desiredDistance = 30f;
						}
						// 根据吃肉进度（eatMeat）在 25 到 15 之间插值缩放期望距离
						desiredDistance *= Mathf.InverseLerp(25f, 15f, player.eatMeat);

						// 质量比
						float massRatio = grabbedChunk.mass / (mainChunk.mass + grabbedChunk.mass);

						// 进入管道时不拉扯；否则如果物体比玩家轻，质量比减半
						if (player.enteringShortCut != null)
						{
							massRatio = 0f;
						}
						else if (grabbed.TotalMass < player.TotalMass)
						{
							massRatio /= 2f;
						}

						// 拉扯条件：不在管道中，或者距离超过期望距离
						if (player.enteringShortCut == null || currentDistance > desiredDistance)
						{
							Vector2 selfMove = direction * ((currentDistance - desiredDistance) * massRatio);
							mainChunk.pos += selfMove;
							mainChunk.vel += selfMove;

							Vector2 grabbedMove = direction * ((currentDistance - desiredDistance) * (1f - massRatio));
							grabbedChunk.pos -= grabbedMove;
							grabbedChunk.vel -= grabbedMove;
						}
						if (player.bodyMode == Player.BodyModeIndex.ClimbingOnBeam &&
							player.animation != Player.AnimationIndex.BeamTip && player.animation != Player.AnimationIndex.StandOnBeam)
						{
							grabbedChunk.vel.y += grabbed.gravity * (1f - grabbedChunk.submersion) * 0.75f;
						}
						if (player.Grabability(grabbed) == Player.ObjectGrabability.Drag && currentDistance > (desiredDistance * 2f) + 30f)
						{
							player.ReleaseGrasp(i);
						}
					}
					else if (actuallyViewed)
					{
						int index = i % 2;
						float toward = (index == 0) ? (-1f) : 1f;

						grabbedChunk.MoveFromOutsideMyUpdate(eu, player.mainBodyChunk.pos + new Vector2(10f * toward, 10f));

						//// 手部跟随图形模块的手
						//if (player.graphicsModule != null && player.graphicsModule is PlayerGraphics playerGraphics)
						//{
						//	//grabbedChunk.vel = playerGraphics.hands[i].vel;
						//	//grabbedChunk.MoveFromOutsideMyUpdate(eu, playerGraphics.hands[i].pos);

						//	grabbedChunk.vel = playerGraphics.hands[index].vel;
						//	grabbedChunk.MoveFromOutsideMyUpdate(eu, playerGraphics.hands[index].pos + new Vector2(3 * toward, 3f));
						//}
						//// 如果抓着武器，设置其旋转方向与手一致，并停止旋转
						//if (grabbed is Weapon grabbedWeapon)
						//{
						//	Vector2 heldItemDirection = player.GetHeldItemDirection(i);
						//	grabbedWeapon.setRotation = new Vector2?(heldItemDirection);
						//	grabbedWeapon.rotationSpeed = 0f;
						//}
					}
					else
					{
						grabbedChunk.pos = player.bodyChunks[0].pos;
						grabbedChunk.vel = mainChunk.vel;
					}
				}
			}
		}

		public static bool Player_CanIPickThisUp(On.Player.orig_CanIPickThisUp orig, Player player, PhysicalObject obj)
		{
			for (int i = 0; i < player.grasps.Length; i++)
			{
				if (player.grasps[i] != null && player.grasps[i].grabbed != null)
				{
					if (player.grasps[i].grabbed == obj)
					{
						return false;
					}
					//if (player.Grabability(player.grasps[i].grabbed) > Player.ObjectGrabability.OneHand)
					//{
					//	num2++;
					//}
				}
			}

			return orig(player, obj);
		}

		public static void PlayerGraphics_ThrowObject(On.PlayerGraphics.orig_ThrowObject orig, PlayerGraphics playerGraphics, int grasp, PhysicalObject obj)
		{
			// 额外槽不走原版手部动画
			if (grasp >= 2)
			{
				return;
			}
			orig(playerGraphics, grasp, obj);
		}

		public static void Player_GrabUpdate(On.Player.orig_GrabUpdate orig, Player player, bool eu)
		{
			player.GetModule(out var module);
			if (module.ExtraGraspAbility)
			{
				if (Plugin.DebugMode && Input.GetKeyDown("c"))
				{
					Log.LogInfo($"grasps");

					if (player.grasps[0] != null || player.grasps[2] != null)
					{
						player.SwitchGrasps(0, 2);
					}
					if (player.grasps[1] != null || player.grasps[3] != null)
					{
						player.SwitchGrasps(1, 3);
					}
				}//

				if (player.input[0].pckp && !player.input[1].pckp && player.switchHandsProcess == 0f && !player.isSlugpup)
				{
					bool flag5 = player.grasps[0] == null && player.grasps[1] == null;
					//if (player.grasps[0] != null && 
					//	(player.Grabability(player.grasps[0].grabbed) == Player.ObjectGrabability.TwoHands || 
					//	player.Grabability(player.grasps[0].grabbed) == Player.ObjectGrabability.Drag))
					//{
					//	flag5 = false;
					//}

					if (flag5)
					{
						if (player.switchHandsCounter == 0)
						{
							player.switchHandsCounter = 15;
						}
						else
						{
							player.room.PlaySound(SoundID.Slugcat_Switch_Hands_Init, player.mainBodyChunk);
							player.switchHandsProcess = 0.01f;
							player.wantToPickUp = 0;
							player.noPickUpOnRelease = 20;
						}
					}
					else
					{
						player.switchHandsProcess = 0f;
					}
				}


				int wantToThrow = player.wantToThrow;
				if (wantToThrow > 0)
				{
					wantToThrow--;
				}
				if (player.input[0].thrw && !player.input[1].thrw && (!ModManager.MSC || !player.monkAscension))
				{
					wantToThrow = 5;
				}
				if (wantToThrow > 0)
				{
					if (ModManager.MSC && MMF.cfgOldTongue.Value && player.grasps[0] == null && player.grasps[1] == null && player.SaintTongueCheck())
					{
					}
					else
					{
						if (player.grasps[0] == null && player.grasps[1] == null)
						{
							for (int i = 0; i < player.grasps.Length; i++)
							{
								if (i >= 2)
								{
									if (player.grasps[i] != null && player.IsObjectThrowable(player.grasps[i].grabbed))
									{
										player.ThrowObject(i, eu);
										player.wantToThrow = 0;
										wantToThrow = 0;

										break;
									}
								}
							}
						}
					}
				}

			}

			orig(player, eu);
		}
		public static void IL_Player_GrabUpdate(ILContext il)
		{
			try
			{
				ILCursor c = new ILCursor(il);

				// if (base.grasps[0] == null && base.grasps[1] == null)
				//IL_1fbe: ldarg.0
				//IL_1fbf: call instance class Creature/Grasp[] Creature::get_grasps()
				//IL_1fc4: ldc.i4.0
				//IL_1fc5: ldelem.ref
				//IL_1fc6: brtrue.s IL_1fd7

				//IL_1fc8: ldarg.0
				//IL_1fc9: call instance class Creature/Grasp[] Creature::get_grasps()
				//IL_1fce: ldc.i4.1
				//IL_1fcf: ldelem.ref
				//IL_1fd0: brtrue.s IL_1fd7 // 非空跳出 IL_1fd7

				// flag6 = false;
				//IL_1fd2: ldc.i4.0
				//IL_1fd3: stloc.s 43
				if (c.TryGotoNext(MoveType.Before,
						i => i.MatchLdarg(0),
						i => i.MatchCall<Creature>("get_grasps"),
						i => i.MatchLdcI4(0),
						i => i.MatchLdelemRef(),
						i => i.Match(OpCodes.Brtrue_S) || i.Match(OpCodes.Brtrue),

						i => i.MatchLdarg(0),
						i => i.MatchCall<Creature>("get_grasps"),
						i => i.MatchLdcI4(1),
						i => i.MatchLdelemRef(),
						i => i.Match(OpCodes.Brtrue_S) || i.Match(OpCodes.Brtrue),

						i => i.MatchLdcI4(0),
						i => i.MatchStloc(43)))
				{
					if (c.TryGotoNext(MoveType.Before, i => i.MatchBrtrue(out _)))
					{
						c.Next.OpCode = c.Next.OpCode == OpCodes.Brtrue_S ? OpCodes.Br_S : OpCodes.Br;
					}
					if (c.TryGotoNext(MoveType.Before, i => i.MatchBrtrue(out _)))
					{
						c.Next.OpCode = c.Next.OpCode == OpCodes.Brtrue_S ? OpCodes.Br_S : OpCodes.Br;
					}
					//c.GotoNext(MoveType.Before, i => i.Match(OpCodes.Brtrue_S) || i.Match(OpCodes.Brtrue));
					//ILLabel? proceedCond = c.Prev.Operand as ILLabel;//跳转指令.Operand  跳转处

					//c.Remove();
					//c.Emit(OpCodes.Br_S, proceedCond);



					//c.GotoNext(MoveType.Before, i => i.Match(OpCodes.Brtrue_S) || i.Match(OpCodes.Brtrue));
					//ILLabel? proceedCond2 = c.Prev.Operand as ILLabel;//跳转指令.Operand  跳转处

					//c.Remove();
					//c.Emit(OpCodes.Br_S, proceedCond2);
				}




				// for (int num28 = 0; num28 < 2; num28++)
				//IL_285a: ldloc.s 54
				//IL_285c: ldc.i4.1
				//IL_285d: add
				//IL_285e: stloc.s 54

				// for (int num28 = 0; num28 < 2; num28++)
				//IL_2860: ldloc.s 54
				//IL_2862: ldc.i4.2
				//IL_2863: blt IL_2691
				if (c.TryGotoNext(MoveType.Before,
						i => i.MatchLdloc(54),
						i => i.MatchLdcI4(2),
						i => i.Match(OpCodes.Blt) || i.Match(OpCodes.Blt_S)))
				{
					// c 现在指向 ldloc.0，把它保留下来
					c.GotoNext(MoveType.Before, i => i.MatchLdcI4(2));  // c 移到 ldc.i4.2 前
					c.Remove();                                          // 只删 ldc.i4.2

					// 此刻栈上已有 [i]（ldloc.0 已执行），压入 length
					c.Emit(OpCodes.Ldarg_0);
					c.Emit(OpCodes.Call, typeof(Creature).GetProperty("grasps").GetGetMethod());
					c.Emit(OpCodes.Ldlen);
					c.Emit(OpCodes.Conv_I4);
					//c.EmitDelegate<Func<Player, int>>(p => p.grasps.Length);
					// 栈变成 [i, length]，下一条 blt 正常判断 i < length

					Log.LogInfo("loop bound -> grasps.Length");
				}
				else
				{
					Log.LogWarning("未找到 i<2 的循环条件，跳过");
				}

				if (Plugin.DebugMode)
					Log.Instance.AppendLogText(il.ToString());
			}
			catch (Exception ex)
			{
				Log.Instance.AppendLogText($"Exception: {ex}");
			}
		}


		public static Vector2 GetHeldItemDirection(On.Player.orig_GetHeldItemDirection orig, Player player, int hand)
		{
			player.GetModule(out var module);
			if (module.ExtraGraspAbility)
			{
				float toward = ((hand % 2) == 0) ? (-1f) : 1f;

				Vector2 vector = Custom.DirVec(player.mainBodyChunk.pos, player.grasps[hand].grabbed.bodyChunks[0].pos) * toward;
				if (player.animation != Player.AnimationIndex.HangFromBeam)
				{
					vector = Custom.PerpendicularVector(vector);
				}
				if (player.bodyMode == Player.BodyModeIndex.Crawl)
				{
					vector = Custom.DirVec(player.bodyChunks[1].pos, Vector2.Lerp(player.grasps[hand].grabbed.bodyChunks[0].pos, player.bodyChunks[0].pos, 0.8f));
				}
				else if (player.animation == Player.AnimationIndex.ClimbOnBeam)
				{
					vector.y = Mathf.Abs(vector.y);
					vector = Vector3.Slerp(vector, Custom.DirVec(player.bodyChunks[1].pos, player.bodyChunks[0].pos), 0.75f);
				}
				else if (player.grasps[hand].grabbed is Spear)
				{
					if (ModManager.CoopAvailable && player.jollyButtonDown && player.handPointing == hand)
					{
						vector = player.PointDir();
					}
					if (player.graphicsModule != null && player.graphicsModule is PlayerGraphics graphics)
					{
						vector = Vector3.Slerp(vector,
							Custom.DegToVec((80f +
							(Mathf.Cos((player.animationFrame + (player.leftFoot ? 9 : 3)) / 12f * 2f * 3.1415927f) * 4f * graphics.spearDir)) * graphics.spearDir),
							Mathf.Abs(graphics.spearDir));
					}
				}
				return vector;
			}
			return orig(player, hand);
		}

		public static void SwapGrasp(this Player player, int a, int b)
		{
			(player.grasps[a], player.grasps[b]) = (player.grasps[b], player.grasps[a]);

			if (player.grasps[a]?.grabbed != null)
			{
				player.SlugcatGrab(player.grasps[a].grabbed, a);
			}
			if (player.grasps[b]?.grabbed != null)
			{
				player.SlugcatGrab(player.grasps[b].grabbed, b);
			}
		}

		//public static void Player_GraphicsModuleUpdated(ILContext il)
		//{
		//	try
		//	{
		//		ILCursor c = new ILCursor(il);

		//		// for (int i = 0; i < 2; i++)
		//		//IL_0621: ldloc.0
		//		//IL_0622: ldc.i4.2
		//		//IL_0623: blt IL_003f
		//		if (c.TryGotoNext(MoveType.Before,
		//				i => i.MatchLdloc(0),
		//				i => i.MatchLdcI4(2),
		//				i => i.Match(OpCodes.Blt) || i.Match(OpCodes.Blt_S)))
		//		{
		//			// c 现在指向 ldloc.0，把它保留下来
		//			c.GotoNext(MoveType.Before, i => i.MatchLdcI4(2));  // c 移到 ldc.i4.2 前
		//			c.Remove();                                          // 只删 ldc.i4.2

		//			// 此刻栈上已有 [i]（ldloc.0 已执行），压入 length
		//			c.Emit(OpCodes.Ldarg_0);
		//			c.EmitDelegate<Func<Player, int>>(p => p.grasps.Length);
		//			// 栈变成 [i, length]，下一条 blt 正常判断 i < length

		//			Log.LogInfo("Player_GraphicsModuleUpdated: loop bound -> grasps.Length");
		//		}
		//		else
		//		{
		//			Log.LogWarning("Player_GraphicsModuleUpdated: 未找到 i<2 的循环条件，跳过");
		//		}

		//		if (Plugin.DebugMode)
		//			Log.Instance.AppendLogText(il.ToString());
		//	}
		//	catch (Exception ex)
		//	{
		//		Log.Instance.AppendLogText($"[Player_GraphicsModuleUpdated] Exception: {ex}");
		//	}
		//}

		/*
		// SwallowObject
		// GraphicsModuleUpdated
		*/
		/*
		CraftingResults
		SpitUpCraftedObject
		CanRetrieveSlugFromBack
		Update
		TerrainImpact
		UpdateAnimation
		GrabUpdate
		SlugcatGrab
		FreeHand
		Regurgitate
		MovementUpdate
		WallJump
		Jump
		CanRetrieveSpearFromBack
		*/

	}
}
