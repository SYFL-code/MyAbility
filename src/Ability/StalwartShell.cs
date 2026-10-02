using BepInEx.Logging;
using CommonUtils.Core;
using HarmonyLib;
using IL;
using LizardCosmetics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using MoreSlugcats;
using MySlugcat.Ability;
using On;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;


namespace MySlugcat.Ability
{
	// 坚韧鳞甲
	public static class StalwartScute
	{
		public sealed class Scute
		{
			public readonly Creature Owner;
			public readonly ScuteData[] Bands;

			// 伤害重复保护
			public Spear? LastDamageSpear;
			public int LastDamageBand = -1;
			public int DamageDuplicateGuard;

			// 偏离重复保护
			public Spear? LastDeflectSpear;
			public int DeflectGrace;

			public Scute(Creature owner)
			{
				this.Owner = owner;

				if (owner is Lizard)
				{
					this.Bands = LizardDefaultScutes;
				}
				else if (owner is Scavenger)
				{
					this.Bands = ScavengerDefaultScutes;
				}
				else
				{
					this.Bands = LizardDefaultScutes;
				}
			}

			public void Update()
			{
				// 伤害重复保护
				if (this.DamageDuplicateGuard > 0)
				{
					this.DamageDuplicateGuard--;
					if (this.DamageDuplicateGuard == 0)
					{
						this.LastDamageSpear = null;
						this.LastDamageBand = -1;
					}
				}

				// 偏离重复保护
				if (this.DeflectGrace > 0)
				{
					this.DeflectGrace--;
					if (this.DeflectGrace == 0)
						this.LastDeflectSpear = null;
				}

				for (int i = 0; i < this.Bands.Length; i++)
					this.Bands[i].Update();
			}

			public static ScuteData[] LizardDefaultScutes =>
			[
				new (0.075f, 0.078f, 0.64f, 0.115f, 7.5f, 0.22f),
				new (0.185f, 0.084f, 0.75f, 0.145f, 10.5f, 0.24f),
				new (0.305f, 0.088f, 0.84f, 0.16f, 12f, 0.26f),
				new (0.43f, 0.089f, 0.88f, 0.17f, 11.5f, 0.26f),
				new (0.56f, 0.074f, 0.72f, 0.16f, 8f, 0.28f),
				new (0.69f, 0.056f, 0.58f, 0.14f, 5.5f, 0.3f),
				new (0.81f, 0.039f, 0.44f, 0.1f, 3.2f, 0.32f)
			];
			public static ScuteData[] ScavengerDefaultScutes =>
			[
				new (0.32f, 0.10f, 0.62f, 0.115f, 6.5f, 0.22f),
				new (0.44f, 0.11f, 0.74f, 0.145f, 9f,   0.24f),
				new (0.56f, 0.11f, 0.82f, 0.16f,  10f,  0.26f),
				new (0.68f, 0.10f, 0.72f, 0.16f,  7.5f, 0.28f),
				new (0.80f, 0.08f, 0.56f, 0.14f,  4.5f, 0.30f),
			];
		}
		extension(Creature creature)
		{
			public Scute Scute => ModuleManager.Get(creature, c => new Scute(c));
		}
		private static readonly ConditionalWeakTable<ScavengerGraphics, ScavengerBodyBands> ScavengerBands = new();

		public sealed class ScuteData
		{
			// center（沿身体的位置 0~1）、width（宽度）、pointDepth/pointShift（下方尖刺形状）、frillLength/frillLean（边缘装饰）
			public float center, width, pointDepth, pointShift, frillLength, frillLean;
			public int damageState;
			// 生成了一块破碎的碎片
			public bool brokenFragmentSpawned;
			// 白色闪烁
			public int whiteFlicker;

			public ScuteData(float center, float width, float pointDepth, float pointShift,
							 float frillLength, float frillLean)
			{
				this.center = center; this.width = width;
				this.pointDepth = pointDepth; this.pointShift = pointShift;
				this.frillLength = frillLength; this.frillLean = frillLean;
			}

			public bool IsPresent => this.damageState < 2;
			public bool IsCracked => this.damageState == 1;
			public bool IsBroken => this.damageState >= 2;

			public void WhiteFlicker(int frames)
			{
				if (frames > this.whiteFlicker)
					this.whiteFlicker = frames;
			}
			public void Update()
			{
				if (this.whiteFlicker > 0)
					this.whiteFlicker--;
			}
			public void TakeHit()
			{
				if (this.damageState < 2)
					this.damageState++;
			}
		}


		// 逻辑
		public static void Creature_Update(On.Creature.orig_Update orig, Creature self, bool eu)
		{
			if (self.Module.StalwartScute)
			{
				if (self is Lizard or Scavenger)
				{
					self.Scute.Update();
				}
			}

			orig(self, eu);
		}

		public static bool Lizard_SpearStick(On.Lizard.orig_SpearStick orig, Lizard self, Weapon source, float dmg,
			BodyChunk chunk, PhysicalObject.Appendage.Pos onAppendagePos, Vector2 direction)
		{
			if (ScuteHost.SpearStick(self, source, chunk, onAppendagePos))
			{
				return false;
			}

			return orig(self, source, dmg, chunk, onAppendagePos, direction);
		}

		public static void Lizard_Violence(On.Lizard.orig_Violence orig, Lizard self, BodyChunk source, Vector2? dirMomentum,
			BodyChunk hitChunk, PhysicalObject.Appendage.Pos onAppendagePos,
			Creature.DamageType type, float damage, float stunBonus)
		{
			bool extraStun = false;

			if (self.Module.StalwartScute)
			{
				if (type == Creature.DamageType.Explosion)
				{
					// 炸碎附近护鳞甲
					ScuteHost.BreakNearbyScutesFromExplosion(self, source, hitChunk);
				}

				var spear = (source?.owner) as Spear;
				if (spear != null && source?.owner is not ExplosiveSpear
					&& hitChunk != null && onAppendagePos == null && type == Creature.DamageType.Stab)
				{
					var scute = self.Scute;

					bool deflected = spear == scute.LastDeflectSpear && scute.DeflectGrace > 0;
					if (!deflected)
						deflected = ScuteArmor.TryGetScuteHit(self, spear, hitChunk, out _, out _, out _);

					if (deflected)
					{
						type = Creature.DamageType.Blunt;
						damage *= 0.1f;
						stunBonus *= 0.4f;
						if (dirMomentum != null) dirMomentum = dirMomentum.Value * 0.33f;
					}
					else
					{
						extraStun = true;
					}
				}
			}

			orig(self, source, dirMomentum, hitChunk, onAppendagePos, type, damage, stunBonus);

			if (extraStun && self.stun < 20) self.Stun(20);
		}

		public static void Scavenger_Violence(On.Scavenger.orig_Violence orig, Scavenger self,
			BodyChunk source, Vector2? dirMomentum, BodyChunk hitChunk,
			PhysicalObject.Appendage.Pos onAppendagePos, Creature.DamageType type, float damage, float stunBonus)
		{
			bool extraStun = false;

			if (self.Module.StalwartScute)
			{
				if (type == Creature.DamageType.Explosion)
				{
					ScuteHost.BreakNearbyScutesFromExplosion(self, source, hitChunk);
				}

				var spear = (source?.owner) as Spear;
				if (spear != null && source?.owner is not ExplosiveSpear
					&& hitChunk != null && onAppendagePos == null && type == Creature.DamageType.Stab)
				{
					var scute = self.Scute;

					bool deflected = spear == scute.LastDeflectSpear && scute.DeflectGrace > 0;
					int band = -1;
					if (!deflected && ScuteArmor.TryGetScuteHit(self, spear, hitChunk, out band, out _, out _))
					{
						deflected = true;
						scute.LastDeflectSpear = spear;
						scute.DeflectGrace = 3;
						ScuteHost.DamageScuteOnce(self, band, spear);
						self.room?.PlaySound(SoundID.Lizard_Head_Shield_Deflect, hitChunk);
					}

					if (deflected)
					{
						// 转为钝击：既减伤，也阻止原版插矛逻辑（只认 Stab）
						type = Creature.DamageType.Blunt;
						damage *= 0.1f;
						stunBonus *= 0.4f;
						if (dirMomentum != null) dirMomentum = dirMomentum.Value * 0.33f;
					}
					else
					{
						extraStun = true;
					}
				}
			}

			orig(self, source, dirMomentum, hitChunk, onAppendagePos, type, damage, stunBonus);

			if (extraStun && self.stun < 20) self.Stun(20);
		}

		// 图形
		public static void Lizard_InitiateGraphicsModule(
			On.Lizard.orig_InitiateGraphicsModule orig, Lizard self)
		{
			orig(self);

			if (!self.Module.StalwartScute) return;
			if (self.graphicsModule is not LizardGraphics lg) return;
			if (FindOn(lg) != null) return;

			var scutes = self.Scute.Bands;
			int start = lg.startOfExtraSprites + lg.extraSprites;   // 下一个可用位置
			//int start = lg.startOfExtraSprites;
			var bands = new BodyBands(lg, start);
			//var bands = new BodyBands(lg, start);
			lg.AddCosmetic(start, bands);
		}
		public static BodyBands? FindOn(LizardGraphics g)
		{
			if (g?.cosmetics == null) return null;
			for (int i = 0; i < g.cosmetics.Count; i++)
				if (g.cosmetics[i] is BodyBands b) return b;
			return null;
		}

		public static void ScavengerGraphics_InitiateSprites(
			On.ScavengerGraphics.orig_InitiateSprites orig, ScavengerGraphics self,
			RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
		{
			orig(self, sLeaser, rCam);

			if (!self.scavenger.Module.StalwartScute) return;
			if (ScavengerBands.TryGetValue(self, out _)) return;

			var bands = new ScavengerBodyBands(self, sLeaser.sprites.Length);
			bands.InitiateSprites(sLeaser, rCam);   // 内部 Array.Resize 追加 sprite
			ScavengerBands.Add(self, bands);
		}
		public static void ScavengerGraphics_DrawSprites(
			On.ScavengerGraphics.orig_DrawSprites orig, ScavengerGraphics self,
			RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
		{
			orig(self, sLeaser, rCam, timeStacker, camPos);
			if (self.culled) return;
			if (ScavengerBands.TryGetValue(self, out var bands))
				bands.DrawSprites(sLeaser, rCam, timeStacker, camPos);
		}
		public static void ScavengerGraphics_ApplyPalette(
			On.ScavengerGraphics.orig_ApplyPalette orig, ScavengerGraphics self,
			RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
		{
			orig(self, sLeaser, rCam, palette);
			if (ScavengerBands.TryGetValue(self, out var bands))
				bands.ApplyPalette(sLeaser, rCam, palette);
		}
		public static void ScavengerGraphics_AddToContainer(
			On.ScavengerGraphics.orig_AddToContainer orig, ScavengerGraphics self,
			RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
		{
			orig(self, sLeaser, rCam, newContatiner);
			if (ScavengerBands.TryGetValue(self, out var bands))
				bands.AddToContainer(sLeaser);
		}


		// 逻辑
		internal static class ScuteHost
		{
			public static bool SpearStick(Lizard lizard, Weapon source,
				BodyChunk chunk, PhysicalObject.Appendage.Pos onAppendagePos)
			{
				if (!lizard.Module.StalwartScute) return false;
				if (source is ExplosiveSpear) return false;

				Spear? spear = source as Spear;
				if (spear == null || chunk == null || onAppendagePos != null) return false;

				if (!ScuteArmor.TryGetScuteHit(lizard, spear, chunk, out int band, out _, out _))
					return false;


				var scute = lizard.Scute;
				scute.LastDeflectSpear = spear;
				scute.DeflectGrace = 3;

				DamageScuteOnce(lizard, band, spear);

				lizard.room?.PlaySound(SoundID.Lizard_Head_Shield_Deflect, chunk);

				return true;
			}

			// 对鳞甲造成一次伤害
			public static void DamageScuteOnce(Creature owner, int bandIndex, Spear spear)
			{
				var scute = owner.Scute;
				if (bandIndex < 0 || bandIndex >= scute.Bands.Length) return;

				var data = scute.Bands[bandIndex];
				if (!data.IsPresent) return;

				if (scute.LastDamageSpear == spear &&
					scute.LastDamageBand == bandIndex &&
					scute.DamageDuplicateGuard > 0) return;

				scute.LastDamageSpear = spear;
				scute.LastDamageBand = bandIndex;
				scute.DamageDuplicateGuard = 8;

				int oldState = data.damageState;

				data.WhiteFlicker(18);
				SpawnVanillaShieldVisuals(owner.room, owner.graphicsModule, spear, 18);
				data.TakeHit();

				if (oldState == 0 && data.damageState == 1) { /* 裂纹 */ }
				else if (oldState == 1 && data.damageState >= 2)
				{
					owner.room?.PlaySound(SoundID.Spear_Fragment_Bounce, owner.mainBodyChunk);
				}
			}

			// 炸碎附近护鳞甲
			public static void BreakNearbyScutesFromExplosion(Creature owner, BodyChunk source, BodyChunk hitChunk)
			{
				var bands = owner.Scute.Bands;
				if (bands.Length == 0) return;

				Vector2 center = source != null ? source.pos
							   : hitChunk != null ? hitChunk.pos
							   : owner.mainBodyChunk.pos;


				int centerBand = -1;
				float bestDist = float.MaxValue;

				for (int i = 0; i < bands.Length; i++)
				{
					Vector2 pos = default;
					if (owner.graphicsModule is ScavengerGraphics sg)
					{
						pos = sg.OnSpinePos(Mathf.Clamp01(bands[i].center), 1f);
					}
					else if (owner.graphicsModule is LizardGraphics lg)
					{
						pos = lg.SpinePosition(bands[i].center, 1f).pos;
					}

					float d = Vector2.Distance(center, pos);
					if (d < bestDist) { bestDist = d; centerBand = i; }
				}
				if (centerBand < 0) centerBand = bands.Length / 2;

				int from = Mathf.Max(0, centerBand - 1);
				int to = Mathf.Min(bands.Length - 1, centerBand + 1);
				for (int i = from; i <= to; i++)
				{
					var b = bands[i];
					if (!b.IsPresent) continue;
					b.damageState = 2;
					b.whiteFlicker = 0;
					b.brokenFragmentSpawned = false;
				}
			}

			// 生成鳞甲的视觉效果
			private static void SpawnVanillaShieldVisuals(Room room, GraphicsModule graphicsModule, Spear spear, int intensity)
			{
				if (room == null || spear?.firstChunk == null) return;
				LizardGraphics? lg = graphicsModule as LizardGraphics;


				Vector2 pos = spear.firstChunk.pos;
				Vector2 vel = spear.firstChunk.vel;
				for (int i = 0; i < intensity; i++)
				{
					room.AddObject(new Spark(
						pos + (Custom.DegToVec(Random.value * 360f) * (5f * Random.value)),
						(vel * -0.1f) + (Custom.DegToVec(Random.value * 360f)
							* (Mathf.Lerp(0.2f, 0.4f, Random.value) * vel.magnitude)),
						Color.white, lg, 10, 170));
				}

				room.AddObject(new StationaryEffect(pos, Color.white, lg, StationaryEffect.EffectType.FlashingOrb));
			}
		}

		// 鳞片甲
		internal static class ScuteArmor
		{
			// 命中判定  返回 true 表示命中鳞甲  false 表示没命中
			// bandIndex 鳞甲序号  spineS 脊柱归一化位置 s ∈ [0,1]  hitUnderbelly 命中腹部而不触发鳞甲
			public static bool TryGetScuteHit(Lizard lizard, Spear spear, BodyChunk hitChunk,
				out int bandIndex, out float spineS, out bool hitUnderbelly)
			{
				bandIndex = -1; spineS = -1f; hitUnderbelly = false;

				if (lizard == null || spear == null || spear.firstChunk == null || hitChunk == null)
					return false;

				var graphics = lizard.graphicsModule as LizardGraphics;
				if (graphics == null) return false;

				var scutes = lizard.Scute.Bands;
				if (scutes == null || scutes.Length == 0) return false;


				Vector2 spearPos = spear.firstChunk.pos;
				Vector2 dir = spear.firstChunk.vel;
				if (dir.sqrMagnitude < 0.01f) dir = spear.firstChunk.pos - spear.firstChunk.lastPos;
				if (dir.sqrMagnitude < 0.01f) dir = hitChunk.pos - spearPos;
				if (dir.sqrMagnitude < 0.0001f) return false;

				Vector2 forward = dir.normalized;
				Vector2 side = new Vector2(-forward.y, forward.x);


				float best = float.MaxValue;
				float bestS = -1f;
				LizardGraphics.LizardSpineData bestSpine = graphics.SpinePosition(0f, 1f);
				float maxDist = hitChunk.rad + 34f;

				for (int i = 0; i <= 160; i++)
				{
					float s = (float)i / 160f;
					var spine = graphics.SpinePosition(s, 1f);
					float d = Vector2.Distance(spine.pos, hitChunk.pos);
					if (d > maxDist) continue;

					Vector2 delta = spine.pos - spearPos;
					float score = Mathf.Abs(Vector2.Dot(delta, side))
								+ (Mathf.Abs(Vector2.Dot(delta, forward)) * 0.015f)
								+ (d * 0.002f);
					if (score < best) { best = score; bestS = s; bestSpine = spine; }
				}

				if (bestS < 0f) return false;
				spineS = bestS;

				for (int i = 0; i < scutes.Length; i++)
				{
					var scute = scutes[i];
					if (!scute.IsPresent) continue;

					float halfWidth = scute.width * 0.5f;
					if (bestS < scute.center - halfWidth || bestS > scute.center + halfWidth)
						continue;


					var bands = FindOn(graphics);
					if (bands == null) return false;

					// 循环内：
					float u = halfWidth <= 0.0001f
						? 0f
						: Mathf.Clamp((bestS - scute.center) / halfWidth, -1f, 1f);

					Vector2 bodyDown = -bands.GetBodyDownForBand(i, u, 1f);
					if (bodyDown.sqrMagnitude < 0.0001f) bodyDown = bestSpine.perp;
					bodyDown.Normalize();

					float rad = Mathf.Max(1f, bestSpine.rad);
					Vector2 toSpine = bestSpine.pos - spearPos;
					float along = Vector2.Dot(toSpine, forward);
					float perpSq = Mathf.Max(0f, toSpine.sqrMagnitude - (along * along));

					Vector2 hitPoint;
					if (perpSq <= rad * rad)
					{
						float off = Mathf.Sqrt((rad * rad) - perpSq);
						hitPoint = spearPos + (forward * (along - off));
					}
					else hitPoint = spear.firstChunk.lastPos;

					float bellyDot = Vector2.Dot(hitPoint - bestSpine.pos, bodyDown) / rad;
					if (bellyDot <= -0.3f) { hitUnderbelly = true; return false; }

					bandIndex = i;
					return true;
				}
				return false;
			}


			// 命中判定（拾荒者版）：返回 true 表示命中鳞甲
			public static bool TryGetScuteHit(Scavenger scav, Spear spear, BodyChunk hitChunk,
				out int bandIndex, out float spineF, out bool hitUnderbelly)
			{
				bandIndex = -1; spineF = -1f; hitUnderbelly = false;

				if (scav == null || spear == null || spear.firstChunk == null || hitChunk == null)
					return false;

				var graphics = scav.graphicsModule as ScavengerGraphics;
				if (graphics == null) return false;

				var scutes = scav.Scute.Bands;
				if (scutes == null || scutes.Length == 0) return false;

				Vector2 spearPos = spear.firstChunk.pos;
				Vector2 dir = spear.firstChunk.vel;
				if (dir.sqrMagnitude < 0.01f) dir = spear.firstChunk.pos - spear.firstChunk.lastPos;
				if (dir.sqrMagnitude < 0.01f) dir = hitChunk.pos - spearPos;
				if (dir.sqrMagnitude < 0.0001f) return false;

				Vector2 forward = dir.normalized;
				Vector2 side = new Vector2(-forward.y, forward.x);

				float best = float.MaxValue;
				float bestF = -1f;
				Vector2 bestSpinePos = default;
				float maxDist = hitChunk.rad + 30f;

				for (int i = 0; i <= 160; i++)
				{
					float f = (float)i / 160f;
					var sp = graphics.OnSpinePos(f, 1f);
					var spinePos = new Vector2(sp.x, sp.y);
					float d = Vector2.Distance(spinePos, hitChunk.pos);
					if (d > maxDist) continue;

					Vector2 delta = spinePos - spearPos;
					float score = Mathf.Abs(Vector2.Dot(delta, side))
								+ (Mathf.Abs(Vector2.Dot(delta, forward)) * 0.015f)
								+ (d * 0.002f);
					if (score < best) { best = score; bestF = f; bestSpinePos = spinePos; }
				}

				if (bestF < 0f) return false;
				spineF = bestF;

				for (int i = 0; i < scutes.Length; i++)
				{
					var scute = scutes[i];
					if (!scute.IsPresent) continue;

					float halfWidth = scute.width * 0.5f;
					if (bestF < scute.center - halfWidth || bestF > scute.center + halfWidth)
						continue;

					var up2 = graphics.OnSpineUpDir(bestF, 1f);
					Vector2 bodyDown = -new Vector2(up2.x, up2.y);
					if (bodyDown.sqrMagnitude < 0.0001f) bodyDown = Vector2.down;
					bodyDown.Normalize();

					float rad = Mathf.Max(1f, graphics.OnSpineWidth(bestF, 1f) * 0.5f);

					Vector2 toSpine = bestSpinePos - spearPos;
					float along = Vector2.Dot(toSpine, forward);
					float perpSq = Mathf.Max(0f, toSpine.sqrMagnitude - (along * along));

					Vector2 hitPoint;
					if (perpSq <= rad * rad)
					{
						float off = Mathf.Sqrt((rad * rad) - perpSq);
						hitPoint = spearPos + (forward * (along - off));
					}
					else hitPoint = spear.firstChunk.lastPos;

					float bellyDot = Vector2.Dot(hitPoint - bestSpinePos, bodyDown) / rad;
					if (bellyDot <= -0.3f) { hitUnderbelly = true; return false; }

					bandIndex = i;
					return true;
				}
				return false;
			}
		}

		// 图形
		public sealed class BodyBands : Template
		{
			public int BandCount => this.scutes.Length;

			private readonly Lizard lizard;
			private readonly ScuteData[] scutes;

			public BodyBands(LizardGraphics lGraphics, int startSprite)
					: base(lGraphics, startSprite)
			{
				this.lizard = lGraphics.lizard;
				this.scutes = lizard.Scute.Bands;

				this.numberOfSprites = this.BandCount * 7;
				this.spritesOverlap = Template.SpritesOverlap.BodySurface;

				this.bandColor = lGraphics.effectColor;
				this.fragmentActive = new bool[this.BandCount];
				this.fragmentPos = new Vector2[this.BandCount];
				this.fragmentLastPos = new Vector2[this.BandCount];
				this.fragmentVel = new Vector2[this.BandCount];
				this.fragmentRotation = new float[this.BandCount];
				this.fragmentRotVel = new float[this.BandCount];
				this.fragmentLife = new int[this.BandCount];
			}

			internal ScuteData GetBand(int index)
				=> this.scutes[Mathf.Clamp(index, 0, this.scutes.Length - 1)];

			private int BandSprite(int bandIndex)
			{
				return this.startSprite + (bandIndex * 7);
			}

			private int CrackSprite(int bandIndex, int segment)
			{
				return this.BandSprite(bandIndex) + 1 + segment;
			}

			private int FragmentSprite(int bandIndex)
			{
				return this.BandSprite(bandIndex) + 1 + 5;
			}

			public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
			{
				for (int i = 0; i < this.BandCount; i++)
				{
					TriangleMesh.Triangle[] array = new TriangleMesh.Triangle[24];
					for (int j = 0; j < 12; j++)
					{
						int num = j * 2;
						int num2 = num + 1;
						int num3 = num + 2;
						int num4 = num + 3;
						array[j * 2] = new TriangleMesh.Triangle(num, num2, num3);
						array[(j * 2) + 1] = new TriangleMesh.Triangle(num2, num4, num3);
					}
					TriangleMesh triangleMesh = new TriangleMesh("Futile_White", array, false, false)
					{
						color = this.bandColor
					};

					sLeaser.sprites[this.BandSprite(i)] = triangleMesh;
					for (int k = 0; k < 5; k++)
					{
						FSprite fsprite = new FSprite("pixel", true)
						{
							anchorX = 0f,
							anchorY = 0.5f,
							scaleY = 1f,
							color = this.crackColor,
							isVisible = false
						};
						sLeaser.sprites[this.CrackSprite(i, k)] = fsprite;
					}
					FSprite fsprite2 = new FSprite("Circle20", true)
					{
						color = this.bandColor,
						scaleX = 0.42f,
						scaleY = 0.16f,
						isVisible = false
					};
					sLeaser.sprites[this.FragmentSprite(i)] = fsprite2;
				}
			}

			public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
			{
				this.bandColor = Color.Lerp(this.lGraphics.effectColor, palette.blackColor, 0.08f);
				this.crackColor = palette.blackColor;

				for (int i = 0; i < this.BandCount; i++)
				{
					if (sLeaser.sprites[this.BandSprite(i)] != null)
					{
						sLeaser.sprites[this.BandSprite(i)].color = this.bandColor;
					}
					for (int j = 0; j < 5; j++)
					{
						if (sLeaser.sprites[this.CrackSprite(i, j)] != null)
						{
							sLeaser.sprites[this.CrackSprite(i, j)].color = this.crackColor;
						}
					}
					if (sLeaser.sprites[this.FragmentSprite(i)] != null)
					{
						sLeaser.sprites[this.FragmentSprite(i)].color = this.bandColor;
					}
				}
			}

			public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
			{
				if (UnityEngine.Random.value > 0.025f)
				{
					this.everySecondDraw = !this.everySecondDraw;
				}

				for (int i = 0; i < this.BandCount; i++)
				{
					ScuteData band = this.GetBand(i);
					TriangleMesh? triangleMesh = sLeaser.sprites[this.BandSprite(i)] as TriangleMesh;
					FSprite fsprite = sLeaser.sprites[this.FragmentSprite(i)];
					bool isPresent = band.IsPresent;
					if (isPresent)
					{
						if (triangleMesh != null)
						{
							this.DrawBandMesh(triangleMesh, i, band, timeStacker, camPos);
							this.ApplyScuteWhiteFlicker(triangleMesh, band);
							triangleMesh.isVisible = true;
						}
						if (band.IsCracked)
						{
							this.DrawCrack(sLeaser, i, band, timeStacker, camPos);
						}
						else
						{
							this.HideCracks(sLeaser, i);
						}
						fsprite.isVisible = false;
					}
					else
					{
						if (triangleMesh != null)
						{
							triangleMesh.isVisible = false;
						}
						this.HideCracks(sLeaser, i);
						if (!band.brokenFragmentSpawned)
						{
							this.StartFallingFragment(i, band, timeStacker);
							band.brokenFragmentSpawned = true;
						}
						this.UpdateAndDrawFragment(fsprite, i, timeStacker, camPos);
					}
				}
			}

			private Vector2 ResolveBodyUpFromSpine(LizardGraphics.LizardSpineData spine, Vector2 fallbackUp)
			{
				Vector2 vector = spine.perp;
				Vector2 vector2;
				if (vector.sqrMagnitude <= 0.0001f)
				{
					if (fallbackUp.sqrMagnitude > 0.0001f)
					{
						vector2 = fallbackUp.normalized;
					}
					else
					{
						vector2 = Vector2.up;
					}
				}
				else
				{
					vector.Normalize();
					Vector2 vector3 = spine.outerPos - spine.pos;
					if (vector3.sqrMagnitude > 0.0001f)
					{
						if (Vector2.Dot(vector, vector3) < 0f)
						{
							vector = -vector;
						}
						vector2 = vector;
					}
					else
					{
						if (fallbackUp.sqrMagnitude > 0.0001f && Vector2.Dot(vector, fallbackUp) < 0f)
						{
							vector = -vector;
						}
						vector2 = vector;
					}
				}
				return vector2;
			}

			private void DrawBandMesh(TriangleMesh mesh, int bandIndex, ScuteData band, float timeStacker, Vector2 camPos)
			{
				Vector2 vector = Vector2.zero;
				bool flag = false;
				for (int i = 0; i < 13; i++)
				{
					float num = i / 12f;
					float num2 = Mathf.Lerp(-1f, 1f, num);
					float num3 = Mathf.Clamp01(band.center + (num2 * band.width * 0.5f));
					LizardGraphics.LizardSpineData lizardSpineData = this.lGraphics.SpinePosition(num3, timeStacker);
					Vector2 vector2 = this.ResolveBodyUpFromSpine(lizardSpineData, flag ? vector : Vector2.zero);
					flag = true;
					vector = vector2;
					float num4 = this.TurnAdjustedBottomHeight(band, num2, timeStacker);
					Vector2 vector3 = this.SurfacePoint(lizardSpineData, vector2, this.TurnAdjustedTopHeight(band, num2, timeStacker));
					Vector2 vector4 = this.SurfacePoint(lizardSpineData, vector2, num4);
					int num5 = i * 2;
					mesh.MoveVertice(num5, vector3 - camPos);
					mesh.MoveVertice(num5 + 1, vector4 - camPos);
				}
			}

			private void DrawCrack(RoomCamera.SpriteLeaser sLeaser, int bandIndex, ScuteData band, float timeStacker, Vector2 camPos)
			{
				float num = (bandIndex % 2 == 0) ? 1f : (-1f);
				float num2 = (float)((bandIndex * 37 % 5) - 2) * 0.018f;
				Vector2 pointInsideBand = this.GetPointInsideBand(bandIndex, -0.3f * num, 0.18f, timeStacker);
				Vector2 pointInsideBand2 = this.GetPointInsideBand(bandIndex, (-0.08f + num2) * num, 0.43f, timeStacker);
				Vector2 pointInsideBand3 = this.GetPointInsideBand(bandIndex, (0.07f - num2) * num, 0.52f, timeStacker);
				Vector2 pointInsideBand4 = this.GetPointInsideBand(bandIndex, 0.3f * num, 0.82f, timeStacker);
				Vector2 pointInsideBand5 = this.GetPointInsideBand(bandIndex, -0.34f * num, 0.62f, timeStacker);
				Vector2 pointInsideBand6 = this.GetPointInsideBand(bandIndex, 0.34f * num, 0.35f, timeStacker);
				this.DrawCrackSegment(sLeaser.sprites[this.CrackSprite(bandIndex, 0)], pointInsideBand, pointInsideBand2, camPos, 1.1f);
				this.DrawCrackSegment(sLeaser.sprites[this.CrackSprite(bandIndex, 1)], pointInsideBand2, pointInsideBand3, camPos, 0.95f);
				this.DrawCrackSegment(sLeaser.sprites[this.CrackSprite(bandIndex, 2)], pointInsideBand3, pointInsideBand4, camPos, 1.05f);
				this.DrawCrackSegment(sLeaser.sprites[this.CrackSprite(bandIndex, 3)], pointInsideBand2, pointInsideBand5, camPos, 0.8f);
				this.DrawCrackSegment(sLeaser.sprites[this.CrackSprite(bandIndex, 4)], pointInsideBand3, pointInsideBand6, camPos, 0.72f);
			}

			private void DrawCrackSegment(FSprite crack, Vector2 from, Vector2 to, Vector2 camPos, float thickness)
			{
				if (crack != null)
				{
					Vector2 vector = to - from;
					crack.x = from.x - camPos.x;
					crack.y = from.y - camPos.y;
					crack.scaleX = vector.magnitude;
					crack.scaleY = thickness;
					crack.rotation = -Mathf.Atan2(vector.y, vector.x) * 57.29578f;
					crack.color = this.crackColor;
					crack.alpha = 1f;
					crack.isVisible = true;
				}
			}

			private void HideCracks(RoomCamera.SpriteLeaser sLeaser, int bandIndex)
			{
				for (int i = 0; i < 5; i++)
				{
					FSprite fsprite = sLeaser.sprites[this.CrackSprite(bandIndex, i)];
					if (fsprite != null)
					{
						fsprite.isVisible = false;
					}
				}
			}

			private Vector2 GetPointInsideBand(int bandIndex, float u, float depth, float timeStacker)
			{
				ScuteData band = this.GetBand(bandIndex);
				u = Mathf.Clamp(u, -0.9f, 0.9f);
				depth = Mathf.Clamp01(depth);
				float bandS = this.GetBandS(bandIndex, u);
				LizardGraphics.LizardSpineData lizardSpineData = this.lGraphics.SpinePosition(bandS, timeStacker);
				Vector2 continuousBodyUpForBand = this.GetContinuousBodyUpForBand(bandIndex, bandS, timeStacker);
				float num = this.TurnAdjustedTopHeight(band, u, timeStacker);
				float num2 = this.TurnAdjustedBottomHeight(band, u, timeStacker);
				float num3 = Mathf.Lerp(num, num2, Mathf.Lerp(0.08f, 0.92f, depth));
				return this.SurfacePoint(lizardSpineData, continuousBodyUpForBand, num3);
			}

			private void StartFallingFragment(int bandIndex, ScuteData band, float timeStacker)
			{
				float center = band.center;
				LizardGraphics.LizardSpineData lizardSpineData = this.lGraphics.SpinePosition(center, timeStacker);
				Vector2 continuousBodyUpForBand = this.GetContinuousBodyUpForBand(bandIndex, center, timeStacker);
				Vector2 vector = this.SurfacePoint(lizardSpineData, continuousBodyUpForBand, 0.65f);
				this.fragmentPos[bandIndex] = vector;
				this.fragmentLastPos[bandIndex] = vector;
				this.fragmentVel[bandIndex] = (continuousBodyUpForBand * 2.5f) +
					(lizardSpineData.dir *UnityEngine.Random.Range(-2.5f, 2.5f)) + new Vector2(0f, 1.5f);
				this.fragmentRotation[bandIndex] = UnityEngine.Random.Range(0f, 360f);
				this.fragmentRotVel[bandIndex] = UnityEngine.Random.Range(-18f, 18f);
				this.fragmentLife[bandIndex] = 70;
				this.fragmentActive[bandIndex] = true;
			}

			private void UpdateAndDrawFragment(FSprite fragment, int bandIndex, float timeStacker, Vector2 camPos)
			{
				if (!this.fragmentActive[bandIndex] || this.fragmentLife[bandIndex] <= 0)
				{
					fragment.isVisible = false;
				}
				else
				{
					this.fragmentLastPos[bandIndex] = this.fragmentPos[bandIndex];
					Vector2[] array = this.fragmentVel;
					array[bandIndex].y = array[bandIndex].y - 0.35f;
					this.fragmentPos[bandIndex] += this.fragmentVel[bandIndex];
					this.fragmentRotation[bandIndex] += this.fragmentRotVel[bandIndex];
					this.fragmentLife[bandIndex]--;

					Room room = this.lizard.room;
					if (room != null)
					{
						IntVector2 tilePosition = room.GetTilePosition(this.fragmentPos[bandIndex]);
						if (room.IsPositionInsideBoundries(tilePosition) && room.GetTile(tilePosition).Solid)
						{
							this.fragmentVel[bandIndex].y = Mathf.Abs(this.fragmentVel[bandIndex].y) * 0.35f;
							Vector2[] array2 = this.fragmentVel;
							array2[bandIndex].x = array2[bandIndex].x * 0.65f;
							Vector2[] array3 = this.fragmentPos;
							array3[bandIndex].y = array3[bandIndex].y + 2f;
						}
					}
					Vector2 vector = Vector2.Lerp(this.fragmentLastPos[bandIndex], this.fragmentPos[bandIndex], timeStacker);
					fragment.x = vector.x - camPos.x;
					fragment.y = vector.y - camPos.y;
					fragment.rotation = this.fragmentRotation[bandIndex];
					fragment.scaleX = 0.42f;
					fragment.scaleY = 0.16f;
					ScuteData band = this.GetBand(bandIndex);
					this.ApplyScuteWhiteFlicker(fragment, band);
					fragment.alpha *= Mathf.InverseLerp(0f, 20f, (float)this.fragmentLife[bandIndex]);
					fragment.isVisible = true;

					if (this.fragmentLife[bandIndex] <= 0)
					{
						this.fragmentActive[bandIndex] = false;
						fragment.isVisible = false;
					}
				}
			}

			private void ApplyScuteWhiteFlicker(FSprite sprite, ScuteData band)
			{
				if (band.whiteFlicker > 0 && (band.whiteFlicker > 15 || this.everySecondDraw))
				{
					sprite.color = Color.white;
				}
				else
				{
					sprite.color = this.bandColor;
				}
				sprite.alpha = 1f;
				if (this.lizard.Template.type == CreatureTemplate.Type.WhiteLizard)
				{
					var lizardGraphics = lizard.graphicsModule as LizardGraphics;
					if (lizardGraphics != null)
					{
						sprite.color = lizardGraphics.whiteCamoColor;
					}
				}
			}

			private float TopHeight(ScuteData band, float u)
			{
				float num = Mathf.InverseLerp(0.76f, 1f, Mathf.Abs(u));
				num = num * num * (3f - (2f * num));
				float num2 = Mathf.Lerp(0.98f, 0.84f, num);
				float frontWrapBoost = this.GetFrontWrapBoost(band);
				float wrapCenterProfile = this.GetWrapCenterProfile(u);
				float num3 = Mathf.Lerp(0.05f, 0.16f, wrapCenterProfile) * frontWrapBoost;
				return num2 + num3;
			}

			private float BottomHeight(ScuteData band, float u)
			{
				float num = Mathf.Clamp(u - band.pointShift, -1f, 1f);
				float num2 = 1f - Mathf.Clamp01(Mathf.Abs(num));
				num2 = num2 * num2 * (3f - (2f * num2));
				float num3 = Mathf.InverseLerp(0.28f, 0.52f, band.pointDepth);
				float num4 = Mathf.Lerp(0.54f, 0.38f, num3);
				float num5 = Mathf.Lerp(0.24f, 0.06f, num3);
				return Mathf.Lerp(num4, num5, num2);
			}

			private float GetBodyTurnAmount(float timeStacker)
			{
				float num = Mathf.Abs(Mathf.Lerp(this.lGraphics.lastDepthRotation, this.lGraphics.depthRotation, timeStacker));
				float num2 = 1f - Mathf.InverseLerp(0.18f, 0.65f, num);
				return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(num2));
			}

			private float GetWrapCenterProfile(float u)
			{
				float num = 1f - Mathf.InverseLerp(0.45f, 1f, Mathf.Abs(u));
				return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(num));
			}

			private float GetFrontWrapBoost(ScuteData band)
			{
				float num = 1f - Mathf.InverseLerp(0.12f, 0.32f, band.center);
				return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(num));
			}

			private float TurnAdjustedTopHeight(ScuteData band, float u, float timeStacker)
			{
				float num = this.GetBodyTurnAmount(timeStacker);
				float frontWrapBoost = this.GetFrontWrapBoost(band);
				num = Mathf.Clamp01(num + (frontWrapBoost * 0.18f));
				float wrapCenterProfile = this.GetWrapCenterProfile(u);
				float num2 = Mathf.Lerp(0.73f, 0.88f, frontWrapBoost);
				float num3 = Mathf.Lerp(1.06f, 1.16f, frontWrapBoost);
				float num4 = Mathf.Lerp(num2, num3, wrapCenterProfile);
				return Mathf.Lerp(this.TopHeight(band, u), num4, num);
			}

			private float TurnAdjustedBottomHeight(ScuteData band, float u, float timeStacker)
			{
				float num = this.GetBodyTurnAmount(timeStacker);
				float frontWrapBoost = this.GetFrontWrapBoost(band);
				num = Mathf.Clamp01(num + (frontWrapBoost * 0.18f));
				float wrapCenterProfile = this.GetWrapCenterProfile(u);
				float num2 = Mathf.Lerp(0.73f, 0.88f, frontWrapBoost);
				float num3 = Mathf.Lerp(1.06f, 1.16f, frontWrapBoost);
				float num4 = Mathf.Lerp(num2, num3, wrapCenterProfile);
				return Mathf.Lerp(this.BottomHeight(band, u), -num4, num);
			}

			internal float GetBandS(int bandIndex, float u)
			{
				ScuteData band = this.GetBand(bandIndex);
				return Mathf.Clamp01(band.center + (Mathf.Clamp(u, -1f, 1f) * band.width * 0.5f));
			}

			internal Vector2 GetBottomEdgePoint(int bandIndex, float u, float timeStacker)
			{
				ScuteData band = this.GetBand(bandIndex);
				float bandS = this.GetBandS(bandIndex, u);
				LizardGraphics.LizardSpineData lizardSpineData = this.lGraphics.SpinePosition(bandS, timeStacker);
				Vector2 continuousBodyUpForBand = this.GetContinuousBodyUpForBand(bandIndex, bandS, timeStacker);
				return this.SurfacePoint(lizardSpineData, continuousBodyUpForBand, this.TurnAdjustedBottomHeight(band, u, timeStacker));
			}

			internal Vector2 GetTopEdgePoint(int bandIndex, float u, float timeStacker)
			{
				ScuteData band = this.GetBand(bandIndex);
				float bandS = this.GetBandS(bandIndex, u);
				LizardGraphics.LizardSpineData lizardSpineData = this.lGraphics.SpinePosition(bandS, timeStacker);
				Vector2 continuousBodyUpForBand = this.GetContinuousBodyUpForBand(bandIndex, bandS, timeStacker);
				return this.SurfacePoint(lizardSpineData, continuousBodyUpForBand, this.TurnAdjustedTopHeight(band, u, timeStacker));
			}

			internal LizardGraphics.LizardSpineData GetBottomEdgeSpine(int bandIndex, float u, float timeStacker)
			{
				return this.lGraphics.SpinePosition(this.GetBandS(bandIndex, u), timeStacker);
			}

			internal Vector2 GetBodyUpForBand(int bandIndex, float u, float timeStacker)
			{
				float bandS = this.GetBandS(bandIndex, u);
				return this.GetContinuousBodyUpForBand(bandIndex, bandS, timeStacker);
			}

			internal Vector2 GetBodyDownForBand(int bandIndex, float u, float timeStacker)
			{
				float bandS = this.GetBandS(bandIndex, u);
				return -this.GetContinuousBodyUpForBand(bandIndex, bandS, timeStacker);
			}

			internal float GetTurnVisibility(LizardGraphics.LizardSpineData spine)
			{
				return 1f;
			}

			private Vector2 GetContinuousBodyUpForBand(int bandIndex, float targetS, float timeStacker)
			{
				ScuteData band = this.GetBand(bandIndex);
				float num = Mathf.Clamp01(band.center - (band.width * 0.5f));
				Vector2 vector = Vector2.zero;
				for (int i = 0; i < 13; i++)
				{
					float num2 = (float)i / 12f;
					float num3 = Mathf.Lerp(num, targetS, num2);
					LizardGraphics.LizardSpineData lizardSpineData = this.lGraphics.SpinePosition(num3, timeStacker);
					vector = this.ResolveBodyUpFromSpine(lizardSpineData, vector);
				}
				return vector;
			}

			private Vector2 SurfacePoint(LizardGraphics.LizardSpineData spine, Vector2 bodyUp, float verticalFraction)
			{
				return spine.pos + (bodyUp * spine.rad * verticalFraction);
			}

			private const float BlackMix = 0.08f;

			private const int BandColumns = 13;

			private const int CrackSegments = 5;

			private const int SpritesPerBand = 7;

			private Color bandColor;

			private Color crackColor = Color.black;

			private bool everySecondDraw;

			private readonly bool[] fragmentActive;

			private readonly Vector2[] fragmentPos;

			private readonly Vector2[] fragmentLastPos;

			private readonly Vector2[] fragmentVel;

			private readonly float[] fragmentRotation;

			private readonly float[] fragmentRotVel;

			private readonly int[] fragmentLife;
		}
		public sealed class ScavengerBodyBands
		{
			public int BandCount => this.scutes.Length;
			public int TotalSprites => this.BandCount * 7;

			private readonly ScavengerGraphics sGraphics;
			private readonly Scavenger scavenger;
			private readonly ScuteData[] scutes;
			private readonly int firstSprite;

			public ScavengerBodyBands(ScavengerGraphics sGraphics, int firstSprite)
			{
				this.sGraphics = sGraphics;
				this.scavenger = sGraphics.scavenger;
				this.scutes = this.scavenger.Scute.Bands;
				this.firstSprite = firstSprite;

				this.bandColor = Color.white;
				this.fragmentActive = new bool[this.BandCount];
				this.fragmentPos = new Vector2[this.BandCount];
				this.fragmentLastPos = new Vector2[this.BandCount];
				this.fragmentVel = new Vector2[this.BandCount];
				this.fragmentRotation = new float[this.BandCount];
				this.fragmentRotVel = new float[this.BandCount];
				this.fragmentLife = new int[this.BandCount];
			}

			private ScuteData GetBand(int index)
				=> this.scutes[Mathf.Clamp(index, 0, this.scutes.Length - 1)];

			private int BandSprite(int i) => this.firstSprite + (i * 7);
			private int CrackSprite(int i, int seg) => this.BandSprite(i) + 1 + seg;
			private int FragmentSprite(int i) => this.BandSprite(i) + 6;

			// ---------- 脊柱采样（拾荒者版核心：三个方法拼出 LizardSpineData 等价物） ----------
			private Vector2 SpinePos(float f, float timeStacker)
			{
				var p = this.sGraphics.OnSpinePos(Mathf.Clamp01(f), timeStacker);
				return new Vector2(p.x, p.y);
			}

			private Vector2 BodyUp(float f, float timeStacker)
			{
				var u = this.sGraphics.OnSpineUpDir(Mathf.Clamp01(f), timeStacker);
				var up = new Vector2(u.x, u.y);
				return up.sqrMagnitude > 0.0001f ? up.normalized : Vector2.up;
			}

			private float SpineRad(float f, float timeStacker)
				=> Mathf.Max(1f, this.sGraphics.OnSpineWidth(Mathf.Clamp01(f), timeStacker) * 0.5f);

			private Vector2 SpineDir(float f, float timeStacker)
			{
				var d = this.sGraphics.OnSpineDir(Mathf.Clamp01(f), timeStacker);
				var dir = new Vector2(d.x, d.y);
				return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.left;
			}

			internal float GetBandS(int bandIndex, float u)
			{
				ScuteData band = this.GetBand(bandIndex);
				return Mathf.Clamp01(band.center + (Mathf.Clamp(u, -1f, 1f) * band.width * 0.5f));
			}

			// ---------- 生命周期 ----------
			public void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
			{
				if (sLeaser.sprites.Length < this.firstSprite + this.TotalSprites)
					Array.Resize(ref sLeaser.sprites, this.firstSprite + this.TotalSprites);

				for (int i = 0; i < this.BandCount; i++)
				{
					TriangleMesh.Triangle[] tris = new TriangleMesh.Triangle[24];
					for (int j = 0; j < 12; j++)
					{
						int n = j * 2;
						tris[j * 2] = new TriangleMesh.Triangle(n, n + 1, n + 2);
						tris[(j * 2) + 1] = new TriangleMesh.Triangle(n + 1, n + 3, n + 2);
					}
					sLeaser.sprites[this.BandSprite(i)] = new TriangleMesh("Futile_White", tris, false, false)
					{ color = this.bandColor };

					for (int k = 0; k < 5; k++)
					{
						sLeaser.sprites[this.CrackSprite(i, k)] = new FSprite("pixel", true)
						{ anchorX = 0f, anchorY = 0.5f, scaleY = 1f, color = this.crackColor, isVisible = false };
					}
					sLeaser.sprites[this.FragmentSprite(i)] = new FSprite("Circle20", true)
					{ color = this.bandColor, scaleX = 0.42f, scaleY = 0.16f, isVisible = false };
				}
			}

			public void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
			{
				// 用拾荒者自己的体色/头色混合出甲片色，已含 darkness 处理
				this.bandColor = Color.Lerp(
					Color.Lerp(this.sGraphics.BlendedBodyColor, this.sGraphics.BlendedHeadColor, 0.45f),
					palette.blackColor, 0.06f);
				this.crackColor = palette.blackColor;

				for (int i = 0; i < this.BandCount; i++)
				{
					if (sLeaser.sprites[this.BandSprite(i)] != null)
						sLeaser.sprites[this.BandSprite(i)].color = this.bandColor;
					for (int j = 0; j < 5; j++)
						if (sLeaser.sprites[this.CrackSprite(i, j)] != null)
							sLeaser.sprites[this.CrackSprite(i, j)].color = this.crackColor;
					if (sLeaser.sprites[this.FragmentSprite(i)] != null)
						sLeaser.sprites[this.FragmentSprite(i)].color = this.bandColor;
				}
			}

			public void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
			{
				if (UnityEngine.Random.value > 0.025f)
					this.everySecondDraw = !this.everySecondDraw;

				for (int i = 0; i < this.BandCount; i++)
				{
					ScuteData band = this.GetBand(i);
					if (sLeaser.sprites[this.BandSprite(i)] is not TriangleMesh mesh) continue;
					var fragment = sLeaser.sprites[this.FragmentSprite(i)];

					if (band.IsPresent)
					{
						this.DrawBandMesh(mesh, i, band, timeStacker, camPos);
						this.ApplyScuteWhiteFlicker(mesh, band);
						mesh.isVisible = true;

						if (band.IsCracked) this.DrawCrack(sLeaser, i, band, timeStacker, camPos);
						else this.HideCracks(sLeaser, i);

						if (fragment != null) fragment.isVisible = false;
					}
					else
					{
						mesh.isVisible = false;
						this.HideCracks(sLeaser, i);

						if (!band.brokenFragmentSpawned)
						{
							this.StartFallingFragment(i, band, timeStacker);
							band.brokenFragmentSpawned = true;
						}
						this.UpdateAndDrawFragment(fragment, i, timeStacker, camPos);
					}
				}
			}

			// 入层：塞进 containers[1]（前肢之后、头/颈/牙之前），并防重复
			public void AddToContainer(RoomCamera.SpriteLeaser sLeaser)
			{
				if (this.TotalSprites == 0 || sLeaser.sprites[this.firstSprite] == null) return;
				if (sLeaser.sprites[this.firstSprite].container != null) return;
				for (int i = 0; i < this.TotalSprites; i++)
					sLeaser.containers[1].AddChild(sLeaser.sprites[this.firstSprite + i]);
			}

			// ---------- 网格/裂纹/碎片（几何与蜥蜴版相同，只换脊柱采样） ----------
			private void DrawBandMesh(TriangleMesh mesh, int bandIndex, ScuteData band, float timeStacker, Vector2 camPos)
			{
				for (int i = 0; i < 13; i++)
				{
					float u = Mathf.Lerp(-1f, 1f, i / 12f);
					float f = this.GetBandS(bandIndex, u);
					Vector2 spinePos = this.SpinePos(f, timeStacker);
					Vector2 up = this.BodyUp(f, timeStacker);
					float rad = this.SpineRad(f, timeStacker);

					Vector2 top = spinePos + (up * (rad * this.TopHeight(band, u)));
					Vector2 bottom = spinePos + (up * (rad * this.BottomHeight(band, u)));
					int n = i * 2;
					mesh.MoveVertice(n, top - camPos);
					mesh.MoveVertice(n + 1, bottom - camPos);
				}
			}

			private void DrawCrack(RoomCamera.SpriteLeaser sLeaser, int bandIndex, ScuteData band, float timeStacker, Vector2 camPos)
			{
				float sign = (bandIndex % 2 == 0) ? 1f : -1f;
				float jitter = (float)((bandIndex * 37 % 5) - 2) * 0.018f;
				Vector2 p0 = this.GetPointInsideBand(bandIndex, -0.3f * sign, 0.18f, timeStacker);
				Vector2 p1 = this.GetPointInsideBand(bandIndex, (-0.08f + jitter) * sign, 0.43f, timeStacker);
				Vector2 p2 = this.GetPointInsideBand(bandIndex, (0.07f - jitter) * sign, 0.52f, timeStacker);
				Vector2 p3 = this.GetPointInsideBand(bandIndex, 0.3f * sign, 0.82f, timeStacker);
				Vector2 p4 = this.GetPointInsideBand(bandIndex, -0.34f * sign, 0.62f, timeStacker);
				Vector2 p5 = this.GetPointInsideBand(bandIndex, 0.34f * sign, 0.35f, timeStacker);
				this.DrawCrackSegment(sLeaser.sprites[this.CrackSprite(bandIndex, 0)], p0, p1, camPos, 1.1f);
				this.DrawCrackSegment(sLeaser.sprites[this.CrackSprite(bandIndex, 1)], p1, p2, camPos, 0.95f);
				this.DrawCrackSegment(sLeaser.sprites[this.CrackSprite(bandIndex, 2)], p2, p3, camPos, 1.05f);
				this.DrawCrackSegment(sLeaser.sprites[this.CrackSprite(bandIndex, 3)], p1, p4, camPos, 0.8f);
				this.DrawCrackSegment(sLeaser.sprites[this.CrackSprite(bandIndex, 4)], p2, p5, camPos, 0.72f);
			}

			private void DrawCrackSegment(FSprite crack, Vector2 from, Vector2 to, Vector2 camPos, float thickness)
			{
				if (crack == null) return;
				Vector2 v = to - from;
				crack.x = from.x - camPos.x;
				crack.y = from.y - camPos.y;
				crack.scaleX = v.magnitude;
				crack.scaleY = thickness;
				crack.rotation = -Mathf.Atan2(v.y, v.x) * 57.29578f;
				crack.color = this.crackColor;
				crack.alpha = 1f;
				crack.isVisible = true;
			}

			private void HideCracks(RoomCamera.SpriteLeaser sLeaser, int bandIndex)
			{
				for (int i = 0; i < 5; i++)
					if (sLeaser.sprites[this.CrackSprite(bandIndex, i)] is FSprite c)
						c.isVisible = false;
			}

			private Vector2 GetPointInsideBand(int bandIndex, float u, float depth, float timeStacker)
			{
				ScuteData band = this.GetBand(bandIndex);
				u = Mathf.Clamp(u, -0.9f, 0.9f);
				depth = Mathf.Clamp01(depth);
				float f = this.GetBandS(bandIndex, u);
				float h = Mathf.Lerp(this.TopHeight(band, u), this.BottomHeight(band, u),
									 Mathf.Lerp(0.08f, 0.92f, depth));
				return this.SpinePos(f, timeStacker) + (this.BodyUp(f, timeStacker) * (this.SpineRad(f, timeStacker) * h));
			}

			private void StartFallingFragment(int bandIndex, ScuteData band, float timeStacker)
			{
				float f = band.center;
				Vector2 pos = this.SpinePos(f, timeStacker)
							+ (this.BodyUp(f, timeStacker) * (this.SpineRad(f, timeStacker) * 0.65f));
				this.fragmentPos[bandIndex] = pos;
				this.fragmentLastPos[bandIndex] = pos;
				this.fragmentVel[bandIndex] = (this.BodyUp(f, timeStacker) * 2.5f)
					+ (this.SpineDir(f, timeStacker) * UnityEngine.Random.Range(-2.5f, 2.5f))
					+ new Vector2(0f, 1.5f);
				this.fragmentRotation[bandIndex] = UnityEngine.Random.Range(0f, 360f);
				this.fragmentRotVel[bandIndex] = UnityEngine.Random.Range(-18f, 18f);
				this.fragmentLife[bandIndex] = 70;
				this.fragmentActive[bandIndex] = true;
			}

			private void UpdateAndDrawFragment(FSprite fragment, int bandIndex, float timeStacker, Vector2 camPos)
			{
				if (fragment == null) return;
				if (!this.fragmentActive[bandIndex] || this.fragmentLife[bandIndex] <= 0)
				{
					fragment.isVisible = false;
					return;
				}

				this.fragmentLastPos[bandIndex] = this.fragmentPos[bandIndex];
				this.fragmentVel[bandIndex].y -= 0.35f;
				this.fragmentPos[bandIndex] += this.fragmentVel[bandIndex];
				this.fragmentRotation[bandIndex] += this.fragmentRotVel[bandIndex];
				this.fragmentLife[bandIndex]--;

				var room = this.scavenger.room;
				if (room != null)
				{
					var tilePos = room.GetTilePosition(this.fragmentPos[bandIndex]);
					if (room.IsPositionInsideBoundries(tilePos) && room.GetTile(tilePos).Solid)
					{
						this.fragmentVel[bandIndex].y = Mathf.Abs(this.fragmentVel[bandIndex].y) * 0.35f;
						this.fragmentVel[bandIndex].x *= 0.65f;
						this.fragmentPos[bandIndex].y += 2f;
					}
				}

				Vector2 drawPos = Vector2.Lerp(this.fragmentLastPos[bandIndex], this.fragmentPos[bandIndex], timeStacker);
				fragment.x = drawPos.x - camPos.x;
				fragment.y = drawPos.y - camPos.y;
				fragment.rotation = this.fragmentRotation[bandIndex];
				fragment.scaleX = 0.42f;
				fragment.scaleY = 0.16f;
				this.ApplyScuteWhiteFlicker(fragment, this.GetBand(bandIndex));
				fragment.alpha *= Mathf.InverseLerp(0f, 20f, this.fragmentLife[bandIndex]);
				fragment.isVisible = true;

				if (this.fragmentLife[bandIndex] <= 0)
				{
					this.fragmentActive[bandIndex] = false;
					fragment.isVisible = false;
				}
			}

			private void ApplyScuteWhiteFlicker(FSprite sprite, ScuteData band)
			{
				if (band.whiteFlicker > 0 && (band.whiteFlicker > 15 || this.everySecondDraw))
					sprite.color = Color.white;
				else
					sprite.color = this.bandColor;
				sprite.alpha = 1f;
			}

			// 形状函数与蜥蜴版保持一致
			private float TopHeight(ScuteData band, float u)
			{
				float edge = Mathf.InverseLerp(0.76f, 1f, Mathf.Abs(u));
				edge = edge * edge * (3f - (2f * edge));
				return Mathf.Lerp(0.98f, 0.84f, edge)
					 + (Mathf.Lerp(0.05f, 0.16f, this.WrapCenterProfile(u)) * this.FrontWrapBoost(band));
			}

			private float BottomHeight(ScuteData band, float u)
			{
				float num = 1f - Mathf.Clamp01(Mathf.Abs(Mathf.Clamp(u - band.pointShift, -1f, 1f)));
				num = num * num * (3f - (2f * num));
				float depth = Mathf.InverseLerp(0.28f, 0.52f, band.pointDepth);
				return Mathf.Lerp(Mathf.Lerp(0.54f, 0.38f, depth), Mathf.Lerp(0.24f, 0.06f, depth), num);
			}

			private float WrapCenterProfile(float u)
			{
				float num = 1f - Mathf.InverseLerp(0.45f, 1f, Mathf.Abs(u));
				return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(num));
			}

			private float FrontWrapBoost(ScuteData band)
			{
				float num = 1f - Mathf.InverseLerp(0.12f, 0.32f, band.center);
				return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(num));
			}

			private Color bandColor;
			private Color crackColor = Color.black;
			private bool everySecondDraw;
			private readonly bool[] fragmentActive;
			private readonly Vector2[] fragmentPos;
			private readonly Vector2[] fragmentLastPos;
			private readonly Vector2[] fragmentVel;
			private readonly float[] fragmentRotation;
			private readonly float[] fragmentRotVel;
			private readonly int[] fragmentLife;
		}


	}
}
