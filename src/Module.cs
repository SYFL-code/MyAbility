using CommonUtils.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MySlugcat
{
	public static class ModuleExtensions
	{
		public static PlayerModule GetModule(this Player player)
		{
			return ModuleManager.Get<Player, PlayerModule>(player, p => new PlayerModule(p));
		}
		public static PlayerModule GetModule(this Player player, out PlayerModule module)
		{
			module = GetModule(player);
			return module;
		}

		public static WeaponModule GetModule(this Weapon weapon)
		{
			return ModuleManager.Get(weapon, w => new WeaponModule(w));
		}
		public static WeaponModule GetModule(this Weapon weapon, out WeaponModule module)
		{
			module = GetModule(weapon);
			return module;
		}
	}

	public class PlayerModule
	{
		private WeakReference<Player> _playerRef;

		public bool PenetrationAbility = false;
		public bool FrameAbility = false;
		public bool ArcLightningAbility = false;
        public bool CamouflageAbility = false;

        //迷彩的实时颜色
        public Color whiteCamoColor = new Color(0f, 0f, 0f);

        //迷彩的目标颜色
        public Color whitePickUpColor;

        public PlayerModule(Player player)
		{
			_playerRef = new WeakReference<Player>(player);

			if (player.slugcatStats.name == SlugcatStats.Name.White)
			{
				PenetrationAbility = true;
				FrameAbility = true;
				ArcLightningAbility = true;
				CamouflageAbility = true;
			}
		}
	}

	public class WeaponModule
	{
		public WeakReference<PhysicalObject?> Owner = new(null);

		// 武器穿透对象
		public WeakReference<PhysicalObject?> stuckInObject = new(null);
		// 武器穿透时长
		public int stuckInObjectTime = 0;
		// 武器穿透次数
		public int penetrateCount = 0;

		public WeaponModule(Weapon weapon)
		{

		}
	}

}
