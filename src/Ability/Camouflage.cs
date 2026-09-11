using CommonUtils.Core;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using Scrap;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MySlugcat.Ability
{
	// 光学迷彩
	public static class Camouflage
	{
		public static CamouflageModule GetCamouflageModule(this Player player)
		{
			return ModuleManager.Get(player, p => new CamouflageModule(p));
		}
		public static CamouflageModule GetCamouflageModule(this Player player, out CamouflageModule module)
		{
			module = GetCamouflageModule(player);
			return module;
		}
		public class CamouflageModule
		{
			public bool isHidden;
			public float timer;
			public int CdTimer;

			public Color? eyeColor;
			public Color? bodyColor;

			//迷彩的实时颜色
			public Color applyColor;
			//迷彩的目标颜色
			public Color pickedColor;

			public CamouflageModule(Player player)
			{
				applyColor = pickedColor = player.room.game.cameras[0].PixelColorAtCoordinate(player.mainBodyChunk.pos);
			}
		}
		public const float oneFrame = 0.016666667f;// 1f / 60f

		public static void Player_Update(On.Player.orig_Update orig, Player player, bool eu)
		{
			orig.Invoke(player, eu);


			player.GetModule(out var playerModule);
			if (playerModule.CamouflageAbility)
			{
				var module = player.GetCamouflageModule();

				if (player.bodyMode == Player.BodyModeIndex.Crawl && player.Consious && module.CdTimer == 0)
				{
					module.timer += oneFrame;
					module.isHidden = true;

					player.Blink(2);
				}
				if (player.bodyMode != Player.BodyModeIndex.Crawl && player.Consious)
				{
					module.timer -= oneFrame;
					module.isHidden = false;
				}

				if (module.CdTimer > 0)
				{
					module.CdTimer--;
					module.timer -= oneFrame;
					module.isHidden = false;
				}

				if (player.room != null)
				{
					module.pickedColor = player.room.game.cameras[0].PixelColorAtCoordinate(player.mainBodyChunk.pos);
				}

				module.applyColor = Color.Lerp(module.applyColor, module.pickedColor, 0.01f);
				module.timer = Mathf.Clamp01(module.timer);

				if (module.isHidden)
				{
					player.slugcatStats.generalVisibilityBonus = 0.1f;
					player.slugcatStats.visualStealthInSneakMode = Mathf.Lerp(1f, 30f, module.timer);
				}
			}
		}
		public static void Player_ThrowObject(On.Player.orig_ThrowObject orig, Player player, int grasp, bool eu)
		{
			if (player != null)
			{
				player.GetModule(out var playerModule);
				if (playerModule.CamouflageAbility)
				{
					var module = player.GetCamouflageModule();

					if (true)
					{
						if (player.grasps[grasp]?.grabbed is Weapon && player.input[0].thrw)
						{
							module.CdTimer = 200;
						}
					}
				}
			}

			orig(player, grasp, eu);
		}

		//玩家显示内容的更新
		//public static void PlayerGraphics_Update(On.PlayerGraphics.orig_Update orig, PlayerGraphics playerGraphics)
		//{
		//	orig.Invoke(playerGraphics);


		//	Player player = playerGraphics.player;
		//	player.GetModule(out var module);

		//	if (module.CamouflageAbility)
		//	{
		//		//如果玩家没死
		//		if (!player.dead)
		//		{
		//			//测他有没有动,我这里是测上上个位置和这次更新的位置的距离是否小于0.3如果小于就说明没动
		//			if (Vector2.Distance(player.mainBodyChunk.pos, player.mainBodyChunk.lastPos) < 0.3)
		//			{
		//				//如果没动就把迷彩现在的颜色渐渐往 迷彩时选择的颜色 靠拢
		//				module.whiteCamoColor = Color.Lerp(module.whiteCamoColor, module.whitePickUpColor, 0.1f);
		//			}
		//			else
		//			{
		//				//不然就把颜色往玩家靠拢
		//				module.whiteCamoColor = Color.Lerp(module.whiteCamoColor, player.ShortCutColor(), 0.1f);
		//			}
		//		}
		//	}
		//}

		//玩家显示的更新
		public static void PlayerGraphics_DrawSprites(On.PlayerGraphics.orig_DrawSprites orig, PlayerGraphics playerGraphics,
			RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
		{
			orig.Invoke(playerGraphics, sLeaser, rCam, timeStacker, camPos);


			Player player = playerGraphics.player;
			player.GetModule(out var playerModule);

			if (playerModule.CamouflageAbility)
			{
				var module = player.GetCamouflageModule();

				module.eyeColor ??= sLeaser.sprites[9].color;
				module.bodyColor ??= sLeaser.sprites[0].color;

				//sLeaser.sprites[9].color = Color.Lerp(module.eyeColor.Value, module.applyColor, module.timer);
				//sLeaser.sprites[9].alpha = 1f - module.timer;

				Color bodyColor = Color.Lerp(module.bodyColor.Value, module.applyColor, module.timer);

				//然后给玩家的身体部件都染上迷彩现在颜色
				for (int i = 0; i < 12; i++)
				{
					// 眼睛
					if (i == 9) continue;

					try
					{
						sLeaser.sprites[i].color = bodyColor;
					}
					catch(Exception e)
					{
						Log.LogError("Error in Camouflage: " + e.Message);
					}
				}
			}
		}



		//[HarmonyPatch(typeof(Player), nameof(Player.VisibilityBonus), MethodType.Getter)]
		//public static class Patch_VisibilityBonus
		//{
		//	// 后置补丁
		//	[HarmonyPostfix]
		//	public static void Postfix(Player __instance, ref float __result)
		//	{
		//		if (__instance == null) return;

		//		try
		//		{
		//			__instance.GetModule(out var module);
		//			if (module.CamouflageAbility)
		//			{
		//				float visibility = (ColorHelper.Lerp(module.whitePickUpColor, __instance.ShortCutColor(), module.whiteCamoColor) * 2f) - 0.7f;
		//				Log.LogInfo($"__result:{__result}, visibility:{visibility}");

		//				__result = Mathf.Min(__result, visibility);
		//			}
		//		}
		//		catch (Exception e)
		//		{
		//			Log.LogError($"[Harmony] VisibilityBonus 补丁出错: {e.Message}");
		//		}
		//	}
		//}

	}
}
