using CommonUtils.Core;
using Menu.Remix.MixedUI;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


namespace MySlugcat
{
	// 可选：添加一个静态类方便访问配置
	public static class MyConfig
	{
		public static float SpawnChance => MyOptions.Instance.spawnChance.Value;
		public static bool StunLand => MyOptions.Instance.stunLand.Value;
		public static float StunDuration => MyOptions.Instance.stunDuration.Value;
        public static string ParticleEffectType => MyOptions.Instance.particleEffectType.Value;
        public static bool EnableLog => MyOptions.Instance.enableLog.Value;
	}

	public class MyOptions : OptionInterface
	{
		public static MyOptions Instance = new MyOptions();
		public static string Name => Plugin.Name;

		public Configurable<float> spawnChance;
		public readonly Configurable<bool> stunLand;
		public readonly Configurable<float> stunDuration;
		public readonly Configurable<string> particleEffectType;

		public readonly Configurable<bool> enableLog;

        public MyOptions()
		{
			Instance = this;

            //设置默认值
            spawnChance = config.Bind<float>($"{Name}_EnderPearl_spawnChance", 0.02f);
			stunLand = config.Bind<bool>($"{Name}_EnderPearl_stunLand", false);
			stunDuration = config.Bind<float>($"{Name}_EnderPearl_stunDuration", 0.5f);
			particleEffectType = config.Bind<string>($"{Name}_EnderPearl_particleEffectType", "Dotted"); // Dotted Runic

			enableLog = config.Bind<bool>($"{Name}_EnderPearl_enableLog", false);
		}

		public override void Initialize()
		{
			base.Initialize();

			OpTab EnderPearlTab = new OpTab(this, "EnderPearl".Translate());
			this.Tabs = new OpTab[]
			{
				EnderPearlTab
			};


			EnderPearlTab.AddItems(
				//标题
				new OpLabel(30f, 560f, "Ender Pearl".Translate(), true),

				// Spawn chance slider (0.00–1.00, in 0.01 steps)
				new OpLabel(30f, 510f, "World spawn chance".Translate()),
				new OpFloatSlider(spawnChance, new Vector2(30f, 470f), 300, 2)
				{
					min = 0f,
					max = 1f,
					description = "Chance (0.00–1.00) that a pearl appears in an eligible room.".Translate()
				},

				new OpLabel(new Vector2(50f, 430f), new Vector2(200f, 24f), "Stun upon landing".Translate()),
				new OpCheckBox(stunLand, new Vector2(30f, 430f))
				{
					description = "Whether the player gets stunned when teleporting.".Translate()
				},

				new OpLabel(new Vector2(50f, 390f), new Vector2(200f, 24f), "Stun duration".Translate()),
				new OpTextBox(stunDuration, new Vector2(30f, 390f), 50f),

				new OpLabel(new Vector2(5f, 350f), new Vector2(200f, 24f), "Particle effect type".Translate()),
				new OpComboBox(particleEffectType, new Vector2(38f, 310f), 150f, Helper.ToListItem(new string[] { "Dotted", "Runic" }).ToList())
				{
					description = "Select the type of particle effect.".Translate()
				},

				new OpLabel(new Vector2(50f, 40f), new Vector2(200f, 24f), "Enable Log".Translate()),
				new OpCheckBox(enableLog, new Vector2(30f, 40f))
				{
					description = "Enable logging.".Translate()
				}

				//// Safe-search radius
				//new OpLabel(30f, 430f, "Safe-landing search radius (tiles)"),
				//new OpUpdown(maxSafeSearchRadius, new Vector2(30f, 400f), 100)
				//{
				//	description = "How far to search for a safe spot if the pearl lands in a wall."
				//},

			//// Teleport damage
			//new OpCheckBox(teleportDamageEnabled, new Vector2(30f, 350f)),
			//new OpLabel(65f, 353f, "Teleport deals 0.5 HP damage"),

			//// Particles
			//new OpCheckBox(particlesEnabled, new Vector2(30f, 310f)),
			//new OpLabel(65f, 313f, "Particle effects on teleport"),

			//// Sound
			//new OpCheckBox(soundEnabled, new Vector2(30f, 270f)),
			//new OpLabel(65f, 273f, "Sound effects on teleport")
			);



			////标题
			//EnderPearlTab.AddItems(new UIelement[]
			//{
			//	new OpLabel(10f, 540f, inGameTranslator.Translate("Ender Pearl"), true)
			//	{
			//		alignment = FLabelAlignment.Left
			//	}
			//});
			////选项
			//EnderPearlTab.AddItems(new UIelement[]
			//{
			//	new OpCheckBox(stunLand, new Vector2(10, 450)),
			//	new OpLabel(new Vector2(75f, 450f), new Vector2(200f, 24f), inGameTranslator.Translate("Stun upon landing"), FLabelAlignment.Left, false, null),
			//	//new OpCheckBox(OpCheckBoxStunDuration, new Vector2(10, 420)),
			//	new OpTextBox(stunDuration, new Vector2(10, 420), 50f),
			//	new OpLabel(new Vector2(75f, 420f), new Vector2(200f, 24f), inGameTranslator.Translate("Stun duration"), FLabelAlignment.Left, false, null),

			//	/*new OpCheckBox(OpCheckBoxSaveIceData_conf, new Vector2(10, 390)),
			//	new OpLabel(new Vector2(50f, 390f), new Vector2(200f, 24f), inGameTranslator.Translate("Save Ice data to the next cycle(Save bug not fixed yet)"), FLabelAlignment.Left, false, null),
			//	new OpCheckBox(OpCheckBoxUnlockIceShieldNum_conf, new Vector2(10, 360)),
			//	new OpLabel(new Vector2(50f, 360f), new Vector2(200f, 24f), inGameTranslator.Translate("Unlock the maximum number of ice shields"), FLabelAlignment.Left, false, null),*/

			//	//new OpLabel(new Vector2(50f, 420f), new Vector2(200f, 24f), inGameTranslator.Translate("If scavenger dies, the players continue playing"), FLabelAlignment.Left, false, null),
			//	/*radioButtonGroup,
			//	radioButton1,
			//	radioButton2*/
			//});

		}

	}
}
