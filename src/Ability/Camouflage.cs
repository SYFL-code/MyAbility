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
		//玩家显示内容的更新
		public static void PlayerGraphics_Update(On.PlayerGraphics.orig_Update orig, PlayerGraphics playerGraphics)
		{
			orig.Invoke(playerGraphics);


			Player player = playerGraphics.player;
			player.GetModule(out var module);

			if (module.CamouflageAbility)
			{
				//如果玩家没死
				if (!player.dead)
				{
					//测他有没有动,我这里是测上上个位置和这次更新的位置的距离是否小于0.3如果小于就说明没动
					if (Vector2.Distance(player.mainBodyChunk.pos, player.mainBodyChunk.lastPos) < 0.3)
					{
						//如果没动就把迷彩现在的颜色渐渐往 迷彩时选择的颜色 靠拢
						module.whiteCamoColor = Color.Lerp(module.whiteCamoColor, module.whitePickUpColor, 0.1f);
					}
					else
					{
						//不然就把颜色往玩家靠拢
						module.whiteCamoColor = Color.Lerp(module.whiteCamoColor, player.ShortCutColor(), 0.1f);
					}
				}
			}
		}

		//玩家显示的更新
		public static void PlayerGraphics_DrawSprites(On.PlayerGraphics.orig_DrawSprites orig, PlayerGraphics playerGraphics,
			RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
		{
			orig.Invoke(playerGraphics, sLeaser, rCam, timeStacker, camPos);


			Player player = playerGraphics.player;
			player.GetModule(out var module);

			if (module.CamouflageAbility)
			{
				//如果玩家动的距离很少
				if (Vector2.Distance(player.mainBodyChunk.pos, player.mainBodyChunk.lastPos) < 0.3)
				{
					//就把玩家迷彩选择的目标色 设为相机在玩家位置获取到的像素颜色
					module.whitePickUpColor = rCam.PixelColorAtCoordinate(player.mainBodyChunk.pos);
				}

				//然后给玩家的身体部件都染上迷彩现在颜色
				for (int i = 0; i < 12; i++)
				{
					if (i == 9) continue;

					try
					{
						sLeaser.sprites[i].color = module.whiteCamoColor;
					}
					catch(Exception e)
					{
						Log.LogError("Error in Camouflage: " + e.Message);
					}
				}
			}
		}



		[HarmonyPatch(typeof(Player), "get_VisibilityBonus")] // 直接按方法名字符串匹配
		public static class Patch_VisibilityBonus
		{
			// 后置补丁
			[HarmonyPostfix]
			public static void Postfix(Player __instance, ref float __result)
			{
				if (__instance == null) return;

				try
				{
					__instance.GetModule(out var module);
					if (module.CamouflageAbility)
					{
						float visibility = (ColorHelper.Lerp(module.whitePickUpColor, __instance.ShortCutColor(), module.whiteCamoColor) * 2f) - 0.8f;
						Log.LogInfo($"[Harmony] get_VisibilityBonus {__result}, {visibility}");

						__result = Mathf.Min(__result, visibility);
					}
				}
				catch (Exception e)
				{
					Log.LogError($"[Harmony] VisibilityBonus 补丁出错: {e.Message}");
				}
			}
		}

	}
}
