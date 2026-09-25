using CommonUtils.Core;
using MoreSlugcats;
using Noise;
using RewiredConsts;
using RWCustom;
using Smoke;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Unity.Microsoft.GDK;
using UnityEngine;
using Watcher;

namespace MySlugcat.Ability
{
	// 爆燃能力
	public static class Deflagration
	{

		public static void Explode(PhysicalObject self, BodyChunk? hitChunk, Creature? thrownBy)
		{
			if (self.slatedForDeletetion)
			{
				return;
			}

			Room room = self.room;
			Color explodeColor = new Color(1f, 0.4f, 0.3f);
			Vector2 vector = Vector2.Lerp(self.firstChunk.pos, self.firstChunk.lastPos, 0.35f);

			room.AddObject(new SootMark(room, vector, 80f, true));

			if (true)//(!this.explosionIsForShow) // 爆炸不只是做做样子
			{
				room.AddObject(new Explosion(room, self, vector, 7, 250f, 6.2f, 2f, 280f, 0.25f, thrownBy, 0.7f, 160f, 1f));
			}

			room.AddObject(new Explosion.ExplosionLight(vector, 280f, 1f, 7, explodeColor));
			room.AddObject(new Explosion.ExplosionLight(vector, 230f, 1f, 3, new Color(1f, 1f, 1f)));
			room.AddObject(new ExplosionSpikes(room, vector, 14, 30f, 9f, 7f, 170f, explodeColor));
			//冲击波效果
			room.AddObject(new ShockWave(vector, 330f, 0.045f, 5, false));
			for (int i = 0; i < 25; i++)
			{
				Vector2 vector2 = Custom.RNV();
				if (room.GetTile(vector + (vector2 * 20f)).Solid)
				{
					if (!room.GetTile(vector - (vector2 * 20f)).Solid)
					{
						vector2 *= -1f;
					}
					else
					{
						vector2 = Custom.RNV();
					}
				}
				for (int j = 0; j < 3; j++)
				{
					room.AddObject(new Spark(vector + (vector2 * Mathf.Lerp(30f, 60f, UnityEngine.Random.value)),
						(vector2 * Mathf.Lerp(7f, 38f, UnityEngine.Random.value)) + (Custom.RNV() * (20f * UnityEngine.Random.value)),
						Color.Lerp(explodeColor, new Color(1f, 1f, 1f), UnityEngine.Random.value), null, 11, 28));
				}
				room.AddObject(new Explosion.FlashingSmoke(vector + (vector2 * (40f * UnityEngine.Random.value)),
					vector2 * Mathf.Lerp(4f, 20f, Mathf.Pow(UnityEngine.Random.value, 2f)),
					1f + (0.05f * UnityEngine.Random.value), new Color(1f, 1f, 1f), explodeColor, UnityEngine.Random.Range(3, 11)));
			}

			BombSmoke? smoke = null;
			if (smoke != null)
			{
				for (int k = 0; k < 8; k++)
				{
					smoke.EmitWithMyLifeTime(vector + Custom.RNV(), Custom.RNV() * (UnityEngine.Random.value * 17f));
				}
			}

			for (int l = 0; l < 6; l++)
			{
				room.AddObject(new ScavengerBomb.BombFragment(vector,
					Custom.DegToVec(((float)l + UnityEngine.Random.value) / 6f * 360f) * Mathf.Lerp(18f, 38f, UnityEngine.Random.value)));
			}
			//屏幕震动
			room.ScreenMovement(new Vector2?(vector), default(Vector2), 1.3f);

			for (int m = 0; m < self.abstractPhysicalObject.stuckObjects.Count; m++)
			{
				self.abstractPhysicalObject.stuckObjects[m].Deactivate();
			}

			//播放爆炸音效
			room.PlaySound(SoundID.Bomb_Explode, vector, self.abstractPhysicalObject);
			//游戏内噪声效果
			room.InGameNoise(new InGameNoise(vector, 9000f, self, 1f));

			bool flag = hitChunk != null;
			for (int n = 0; n < 5; n++)
			{
				if (room.GetTile(vector + (Custom.fourDirectionsAndZero[n].ToVector2() * 20f)).Solid)
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				if (smoke == null)
				{
					smoke = new BombSmoke(room, vector, null, explodeColor);
					room.AddObject(smoke);
				}
				if (hitChunk != null)
				{
					smoke.chunk = hitChunk;
				}
				else
				{
					smoke.chunk = null;
					smoke.fadeIn = 1f;
				}
				smoke.pos = vector;
				smoke.stationary = true;
				smoke.DisconnectSmoke();
			}
			else if (smoke != null)
			{
				smoke.Destroy();
			}

			//this.Destroy();
		}


		public static bool Deflagration_HitSomething<O, W>(O orig_, W weapon, SharedPhysics.CollisionResult result, bool eu)
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
				if (thrownBy.GetModule().DeflagrationAbility)
				{
					if (result.obj is Creature hitCreature)
					{
						weaponModule.stuckInObject.TryGetTarget(out var stuckInObject);
						if (stuckInObject != hitCreature || weaponModule.stuckInObjectTime > 30)
						{
							float percentage = 8;
							if (weapon is Spear)
							{
								percentage = 12;
								if (thrownBy.GetModule().ArcLightningAbility)
								{
									percentage = 0.1f;
								}
							}
							else if (weapon is Rock)
							{
								percentage = 8;
							}
							else if (weapon is ScavengerBomb)
							{
								percentage = 60;
							}
							else if (weapon is PuffBall)
							{
								percentage = 14;
							}
							else if (ModManager.MSC && weapon is LillyPuck)
							{
								percentage = 8;
							}
							else if (ModManager.Watcher && weapon is Boomerang)
							{
								percentage = 16;
							}

							if (percentage > UnityEngine.Random.Range(0, 100))
							{
								Explode(weapon, result.chunk, thrownBy);
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

		public static void Player_Die(On.Player.orig_Die orig, Player player)
		{
			bool wasDead = player.dead;

			orig(player);


			Creature creature = player;
			if (!wasDead && player.dead && creature.GetModule().DeflagrationAbility)
			{
				if (100 > UnityEngine.Random.Range(0, 100))
				{
					Explode(player, null, player);
				}
			}
		}

	}
}
