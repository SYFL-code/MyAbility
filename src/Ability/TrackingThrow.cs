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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using UnityEngine;
using Watcher;

namespace MySlugcat.Ability
{
	// 追踪投掷
	public static class TrackingThrow
	{
		public static void Weapon_Thrown(On.Weapon.orig_Thrown orig, Weapon weapon, Creature thrownBy, Vector2 thrownPos,
			Vector2? firstFrameTraceFromPos, IntVector2 throwDir, float frc, bool eu)
		{
			weapon.GetModule(out var weaponModule);
			if (weaponModule.Owner.TryGetTarget(out var owner) && owner is Player player)
			{
				if (player.GetModule().TrackingThrowAbility)
				{
					Vector2 startPos = weapon.firstChunk.pos;
					Vector2 vel = weapon.firstChunk.vel;

					if (vel.sqrMagnitude >= 0.01f)
					{
						List<Creature> candidates = Helper.FindCreaturesInCone(startPos, weapon.firstChunk.vel.normalized, weapon.room,
							80f, 30f * 20f, [thrownBy], [thrownBy.GetType()]);

						if (candidates != null && candidates.Count > 0)
						{
							candidates.Sort((a, b) =>
							{
								float da = Vector2.Distance(startPos, a.mainBodyChunk.pos);
								float db = Vector2.Distance(startPos, b.mainBodyChunk.pos);
								return da.CompareTo(db);
							});


							Creature target = candidates[0];
							Vector2 currentPos = weapon.firstChunk.pos;
							Vector2 toTarget = target.mainBodyChunk.pos - currentPos;

							if (toTarget.sqrMagnitude > 40f) // 距离太近就不追，防抖动
							{
								float speed = weapon.firstChunk.vel.magnitude;
								if (speed >= 0.01f)
								{
									Vector2 currentDir = weapon.firstChunk.vel / speed;
									Vector2 desiredDir = toTarget.normalized;

									float turnSpeed = 0.15f; // 转向快慢，自己调
									turnSpeed = Debugger.floats[0, 0.15f, "turnSpeed"];

									Vector2 newDir = Vector2.Lerp(currentDir, desiredDir, turnSpeed).normalized;

									weapon.firstChunk.vel = newDir * speed; // 只改方向，速度不变
								}
							}
						}
					}
				}
			}

			orig(weapon, thrownBy, thrownPos, firstFrameTraceFromPos, throwDir, frc, eu);
		}

	}
}
