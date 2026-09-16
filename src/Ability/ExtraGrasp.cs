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
		public static void PlayerGraphics_ctor(On.PlayerGraphics.orig_ctor orig, PlayerGraphics graphics, PhysicalObject ow)
		{
			orig.Invoke(graphics, ow);

			if (ow is Player player)
			{
				player.GetModule(out var module);
				if (module.ExtraGraspAbility)
				{
					Array.Resize(ref graphics.hands, graphics.hands.Length + ExtraGraspsCount);

					for (int i = 2; i < graphics.hands.Length; i++)
					{
						graphics.hands[i] = graphics.hands[i % 2];
					}
				}
			}
		}

		public static void Creature_SwitchGrasps(On.Creature.orig_SwitchGrasps orig, Creature creature, int a, int b)
		{
			if (creature is Player player)
			{
				player.GetModule(out var module);
				if (module.ExtraGraspAbility)
				{
					if (player.input[0].y > 0)
					{
						bool moved = false;

						if (player.grasps[0] != null)
						{
							if (player.grasps[0].grabbed is not Spear)
							{
								for (int i = 0; i < player.grasps.Length; i++)
								{
									if (i >= 2)
									{
										if (player.grasps[i] == null)
										{
											player.grasps[i] = player.grasps[0];
											player.grasps[0] = null;
											moved = true;
											break;
										}
									}
								}
							}
						}
						else
						{
							for (int i = 0; i < player.grasps.Length; i++)
							{
								if (i >= 2)
								{
									if (player.grasps[i] != null)
									{
										player.grasps[0] = player.grasps[i];
										player.grasps[i] = null;
										moved = true;
										break;
									}
								}
							}
						}

						if (moved)
						{
							player.UpdateGraspIndexes();

							return;
						}
						if (player.grasps[0] != null && player.grasps[0].grabbed is not Spear)
						{
							if (player.grasps.Length > 2)
							{
								orig(creature, 0, 2);

								return;
							}
						}
						else
						{
							if (player.grasps[1] != null && player.grasps[1].grabbed is not Spear)
							{
								if (player.grasps.Length > 2)
								{
									orig(creature, 1, 2);

									return;
								}
							}
						}

					}
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

						//grabbedChunk.MoveFromOutsideMyUpdate(eu, player.mainBodyChunk.pos + new Vector2(10f * toward, 10f));


						// 手部跟随图形模块的手
						if (player.graphicsModule != null && player.graphicsModule is PlayerGraphics playerGraphics)
						{
							Vector2 anchor = playerGraphics.head.pos + new Vector2(10f * toward, 3f);
							grabbedChunk.MoveFromOutsideMyUpdate(eu, anchor);

							//grabbedChunk.vel = playerGraphics.hands[i].vel;
							//grabbedChunk.MoveFromOutsideMyUpdate(eu, playerGraphics.hands[i].pos);

							grabbedChunk.vel = playerGraphics.hands[index].vel;
							//grabbedChunk.MoveFromOutsideMyUpdate(eu, playerGraphics.hands[index].pos + new Vector2(3 * toward, 3f));
						}

						// 如果抓着武器，设置其旋转方向与手一致，并停止旋转
						if (grabbed is Weapon grabbedWeapon)
						{
							Vector2 heldItemDirection = player.GetHeldItemDirection(i);
							grabbedWeapon.setRotation = new Vector2?(heldItemDirection);
							grabbedWeapon.rotationSpeed = 0f;
						}
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

			player.GetModule(out var module);
			if (module.ExtraGraspAbility)
			{
				if (player.grasps[0] != null && player.grasps[1] != null)
				{
					if (player.Grabability(obj) > Player.ObjectGrabability.OneHand)
					{
						return false;
					}
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

				//if (player.input[0].pckp && !player.input[1].pckp && player.switchHandsProcess == 0f && !player.isSlugpup)
				//{

				//}

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


				/*
				//int wantToThrow = player.wantToThrow;
				//if (wantToThrow > 0)
				//{
				//	wantToThrow--;
				//}
				//if (player.input[0].thrw && !player.input[1].thrw && (!ModManager.MSC || !player.monkAscension))
				//{
				//	wantToThrow = 5;
				//}
				//if (wantToThrow > 0)
				//{
				//	if (ModManager.MSC && MMF.cfgOldTongue.Value && player.grasps[0] == null && player.grasps[1] == null && player.SaintTongueCheck())
				//	{
				//	}
				//	else
				//	{
				//		if (player.grasps[0] == null && player.grasps[1] == null)
				//		{
				//			for (int i = 0; i < player.grasps.Length; i++)
				//			{
				//				if (i >= 2)
				//				{
				//					if (player.grasps[i] != null && player.IsObjectThrowable(player.grasps[i].grabbed))
				//					{
				//						player.ThrowObject(i, eu);
				//						player.wantToThrow = 0;
				//						wantToThrow = 0;

				//						break;
				//					}
				//				}
				//			}
				//		}
				//	}
				//}
				*/
			}

			orig(player, eu);
		}
		public static void IL_Player_GrabUpdate(ILContext il)
		{
			try
			{
				ILCursor c = new ILCursor(il);


				// GraspsCanBeCrafted 抓握可以制作
				// if (ModManager.MSC && (FreeHand() == -1 || SlugCatClass == MoreSlugcatsEnums.SlugcatStatsName.Artificer) && GraspsCanBeCrafted())
				//IL_05cf: ldsfld bool ModManager::MSC
				//IL_05d4: brfalse.s IL_0605

				//IL_05d6: ldarg.0
				//IL_05d7: call instance int32 Player::FreeHand()
				//IL_05dc: ldc.i4.m1
				//IL_05dd: beq.s IL_05f1 // 跳进

				//IL_05df: ldarg.0
				//IL_05e0: ldfld class SlugcatStats/Name Player::SlugCatClass
				//IL_05e5: ldsfld class SlugcatStats/Name MoreSlugcats.MoreSlugcatsEnums/SlugcatStatsName::Artificer
				//IL_05ea: call bool class ExtEnum`1<class SlugcatStats/Name>::op_Equality(class ExtEnum`1<!0>, class ExtEnum`1<!0>)
				////IL_05ef: brfalse.s IL_0605
				if (c.TryGotoNext(MoveType.Before,
					i => i.MatchLdsfld<ModManager>(nameof(ModManager.MSC)),
					i => i.Match(OpCodes.Brfalse) || i.Match(OpCodes.Brfalse_S),

					i => i.MatchLdarg(0),
					i => i.MatchCall<Player>(nameof(Player.FreeHand)),
					i => i.MatchLdcI4(-1),
					i => i.Match(OpCodes.Beq) || i.Match(OpCodes.Beq_S)))
				{
					// c 现在指向 ldarg.0，把它保留下来
					c.GotoNext(MoveType.After, i => i.MatchLdarg(0));  // 移到 ldarg.0 后
					c.Remove();


					c.EmitDelegate<Func<Player, int>>(player =>
					{
						player.GetModule(out var module);
						if (module.ExtraGraspAbility)
						{
							if (player.grasps[0] != null && player.HeavyCarry(player.grasps[0].grabbed))
							{
								return -1;
							}
							for (int i = 0; i < 2; i++)
							{
								if (player.grasps[i] == null && (!player.isSlugpup || i != 1))
								{
									return i;
								}
							}
							return -1;
						}
						return player.FreeHand();
					});

					Log.LogInfo("[GraspsCanBeCrafted] FreeHand() -> new FreeHand()");
				}
				else
				{
					Log.LogWarning("[GraspsCanBeCrafted] 未找到条件，跳过");
				}



				// ThrowObject 投掷物体
				// for (int num16 = 0; num16 < 2; num16++)
				//IL_1d9f: ldloc.s 39
				//IL_1da1: ldc.i4.1
				//IL_1da2: add
				//IL_1da3: stloc.s 39

				// for (int num16 = 0; num16 < 2; num16++)
				//IL_1da5: ldloc.s 39
				//IL_1da7: ldc.i4.2
				//IL_1da8: blt.s IL_1d6c
				if (c.TryGotoNext(MoveType.Before,
						i => i.MatchLdloc(39),
						i => i.MatchLdcI4(2),
						i => i.Match(OpCodes.Blt) || i.Match(OpCodes.Blt_S)))
				{
					// c 现在指向 ldloc.0，把它保留下来
					c.GotoNext(MoveType.Before, i => i.MatchLdcI4(2));  // c 移到 ldc.i4.2 前
					c.Remove();                                          // 只删 ldc.i4.2

					// 此刻栈上已有 [i]（ldloc.0 已执行），压入 length
					c.Emit(OpCodes.Ldarg_0);
					c.Emit(OpCodes.Call, typeof(Creature).GetProperty(nameof(Creature.grasps)).GetGetMethod());
					c.Emit(OpCodes.Ldlen);
					c.Emit(OpCodes.Conv_I4);
					//c.EmitDelegate<Func<Player, int>>(p => p.grasps.Length);
					// 栈变成 [i, length]，下一条 blt 正常判断 i < length

					Log.LogInfo("[ThrowObject] loop bound -> grasps.Length");
				}
				else
				{
					Log.LogWarning("[ThrowObject] 未找到 i<2 的循环条件，跳过");
				}

				/*
				if (this.animation == Player.AnimationIndex.DeepSwim)
				{
					if (base.grasps[0] == null && base.grasps[1] == null)
					{
						flag7 = false;
					}
				*/
				/*
				// CanReleaseObject 允许放下
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
				//if (c.TryGotoNext(MoveType.Before,
				//		i => i.MatchLdarg(0),
				//		i => i.MatchCall<Creature>("get_grasps"),
				//		i => i.MatchLdcI4(0),
				//		i => i.MatchLdelemRef(),
				//		i => i.Match(OpCodes.Brtrue_S) || i.Match(OpCodes.Brtrue),

				//		i => i.MatchLdarg(0),
				//		i => i.MatchCall<Creature>("get_grasps"),
				//		i => i.MatchLdcI4(1),
				//		i => i.MatchLdelemRef(),
				//		i => i.Match(OpCodes.Brtrue_S) || i.Match(OpCodes.Brtrue),

				//		i => i.MatchLdcI4(0),
				//		i => i.MatchStloc(43)))
				//{
				//	for (int i = 0; i < 12; i++)
				//	{
				//		c.Remove();
				//	}

				//	//c.GotoNext(MoveType.Before, i => i.Match(OpCodes.Brtrue_S) || i.Match(OpCodes.Brtrue));
				//	//ILLabel? proceedCond = c.Next.Operand as ILLabel;//跳转指令.Operand  跳转处

				//	//c.Remove();
				//	//c.Emit(OpCodes.Br_S, proceedCond);



				//	//c.GotoNext(MoveType.Before, i => i.Match(OpCodes.Brtrue_S) || i.Match(OpCodes.Brtrue));
				//	//ILLabel? proceedCond2 = c.Next.Operand as ILLabel;//跳转指令.Operand  跳转处

				//	//c.Remove();
				//	//c.Emit(OpCodes.Br_S, proceedCond2);

				//	Log.LogInfo("[CanReleaseObject] loop bound -> null");
				//}
				//else
				//{
				//	Log.LogWarning("[CanReleaseObject] 未找到条件，跳过");
				//}
				*/




				// ReleaseObject 放下
				// for (int num22 = 0; num22 < 2; num22++)
				//IL_21ab: ldloc.s 47
				//IL_21ad: ldc.i4.1
				//IL_21ae: add
				//IL_21af: stloc.s 47

				// for (int num22 = 0; num22 < 2; num22++)
				//IL_21b1: ldloc.s 47
				//IL_21b3: ldc.i4.2
				//IL_21b4: blt.s IL_219a
				if (c.TryGotoNext(MoveType.Before,
						i => i.MatchLdloc(47),
						i => i.MatchLdcI4(2),
						i => i.Match(OpCodes.Blt) || i.Match(OpCodes.Blt_S)))
				{
					// c 现在指向 ldloc.0，把它保留下来
					c.GotoNext(MoveType.Before, i => i.MatchLdcI4(2));  // c 移到 ldc.i4.2 前
					c.Remove();                                          // 只删 ldc.i4.2

					// 此刻栈上已有 [i]（ldloc.0 已执行），压入 length
					c.Emit(OpCodes.Ldarg_0);
					c.Emit(OpCodes.Call, typeof(Creature).GetProperty(nameof(Creature.grasps)).GetGetMethod());
					c.Emit(OpCodes.Ldlen);
					c.Emit(OpCodes.Conv_I4);
					//c.EmitDelegate<Func<Player, int>>(p => p.grasps.Length);
					// 栈变成 [i, length]，下一条 blt 正常判断 i < length

					Log.LogInfo("[ReleaseObject] loop bound -> grasps.Length");
				}
				else
				{
					Log.LogWarning("[ReleaseObject] 未找到 i<2 的循环条件，跳过");
				}



				// SlugcatGrab 抓握
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
					c.Emit(OpCodes.Call, typeof(Creature).GetProperty(nameof(Creature.grasps)).GetGetMethod());
					c.Emit(OpCodes.Ldlen);
					c.Emit(OpCodes.Conv_I4);
					//c.EmitDelegate<Func<Player, int>>(p => p.grasps.Length);
					// 栈变成 [i, length]，下一条 blt 正常判断 i < length

					Log.LogInfo("[SlugcatGrab] loop bound -> grasps.Length");
				}
				else
				{
					Log.LogWarning("[SlugcatGrab] 未找到 i<2 的循环条件，跳过");
				}


				if (Plugin.DebugMode)
					Log.Instance.AppendLogText(il.ToString());
			}
			catch (Exception ex)
			{
				Log.Instance.AppendLogText($"Exception: {ex}");
			}
		}

		public static void IL_PlayerGraphics_Update(ILContext il)
		{
			try
			{
				ILCursor c = new ILCursor(il);


				// TubeWorm
				// for (int n = 0; n < player.grasps.Length; n++)
				//IL_32f9: ldloc.s 37
				//IL_32fb: ldc.i4.1
				//IL_32fc: add
				//IL_32fd: stloc.s 37

				// for (int n = 0; n < player.grasps.Length; n++)
				//IL_32ff: ldloc.s 37
				//IL_3301: ldarg.0
				//IL_3302: ldfld class Player PlayerGraphics::player
				//IL_3307: callvirt instance class Creature/Grasp[] Creature::get_grasps()
				//IL_330c: ldlen
				//IL_330d: conv.i4
				//IL_330e: blt IL_328e
				if (c.TryGotoNext(MoveType.Before,
						i => i.MatchLdloc(37),
						i => i.MatchLdarg(0),
						i => i.MatchLdfld<PlayerGraphics>(nameof(PlayerGraphics.player)),
						i => i.MatchCallOrCallvirt<Creature>("get_" + nameof(Creature.grasps)),
						i => i.MatchLdlen(),
						i => i.MatchConvI4(),
						i => i.Match(OpCodes.Blt) || i.Match(OpCodes.Blt_S)))
				{
					// c 现在指向 ldloc.0，把它保留下来
					c.GotoNext(MoveType.After, i => i.MatchLdloc(37));  // c 移到 ldloc.0 后
					for (int i = 0; i < 5; i++)
					{
						c.Remove();
					}

					// 此刻栈上已有 [i]（ldloc.0 已执行），压入 2
					//IL_2862: ldc.i4.2
					c.Emit(OpCodes.Ldc_I4_2);
					// 栈变成 [i, 2]，下一条 blt 正常判断 i < 2

					Log.LogInfo("[TubeWorm] loop bound -> 2");
				}
				else
				{
					Log.LogWarning("[TubeWorm] 未找到 i<player.grasps.Length 的循环条件，跳过");
				}


				if (Plugin.DebugMode)
					Log.Instance.AppendLogText(il.ToString());
			}
			catch (Exception ex)
			{
				Log.Instance.AppendLogText($"Exception: {ex}");
			}
		}
		public static void IL_Player_SpitUpCraftedObject(ILContext il)

		{
			try
			{
				ILCursor c = new ILCursor(il);


				// SpitUpCraftedObject grasps.Length
				// for (int i = 0; i < base.grasps.Length; i++)
				//IL_01ec: ldloc.1
				//IL_01ed: ldc.i4.1
				//IL_01ee: add
				//IL_01ef: stloc.1

				// for (int i = 0; i < base.grasps.Length; i++)
				//IL_01f0: ldloc.1
				//IL_01f1: ldarg.0
				//IL_01f2: call instance class Creature/Grasp[] Creature::get_grasps()
				//IL_01f7: ldlen
				//IL_01f8: conv.i4
				//IL_01f9: blt IL_003a



				// for (int j = 0; j < base.grasps.Length; j++)
				//IL_049f: ldloc.s 5
				//IL_04a1: ldc.i4.1
				//IL_04a2: add
				//IL_04a3: stloc.s 5

				//// for (int j = 0; j < base.grasps.Length; j++)
				//IL_04a5: ldloc.s 5
				//IL_04a7: ldarg.0
				//IL_04a8: call instance class Creature/Grasp[] Creature::get_grasps()
				//IL_04ad: ldlen
				//IL_04ae: conv.i4
				//IL_04af: blt IL_038d

				int k = 0;
				while (c.TryGotoNext(MoveType.Before,
					//i => i.MatchLdloc(ldloc),
					i => i.MatchLdarg(0),
					i => i.MatchCallOrCallvirt<Creature>("get_" + nameof(Creature.grasps)),
					i => i.MatchLdlen(),
					i => i.MatchConvI4(),
					i => i.Match(OpCodes.Blt) || i.Match(OpCodes.Blt_S)))
				{
					// c 现在指向 ldloc.0，把它保留下来
					//c.GotoNext(MoveType.Before, i => i.MatchLdarg(0));  // c 移到 ldarg.0 前
					for (int i = 0; i < 4; i++)
					{
						c.Remove();
					}

					// 此刻栈上已有 [i]（ldloc.0 已执行），压入 2
					//IL_2862: ldc.i4.2
					c.Emit(OpCodes.Ldc_I4_2);
					// 栈变成 [i, 2]，下一条 blt 正常判断 i < 2

					k++;
					Log.LogInfo("[SpitUpCraftedObject] loop bound -> 2");
				}
				if (k == 0)
				{
					Log.LogWarning("[SpitUpCraftedObject] 未找到 i<player.grasps.Length 的循环条件，跳过");
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

		/*
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
		*/

		// SwallowObject
		// GraphicsModuleUpdated
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
