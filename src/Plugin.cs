using BepInEx;
using BepInEx.Logging;
using CommonUtils;
using CommonUtils.Core;
using Fisobs;
using Fisobs.Core;
using Fisobs.Items;
using IL;
using Menu.Remix;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MoreSlugcats;
using MySlugcat.Ability;
using Noise;
using On;
using RewiredConsts;
using RWCustom;
using Scrap;
using Smoke;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;
using UnityEngine;
using Watcher;
using static PhysicalObject;
using static RainWorld;

namespace MySlugcat;
[BepInPlugin(Plugin.GUID, Plugin.NAME, Plugin.VERSION)]
public sealed class Plugin : BaseUnityPlugin
{
	#region 信息
	public const string GUID = "myslugcat.redlyn";
	public const string NAME = "My Slugcat";
	public const string VERSION = "0.1.0";

	//public const string version = "v01";
	public const string Name = "MySlugcat";

	public static string version = BuildInfo.Version;
	public static string buildTime = BuildInfo.BuildTime;
	#endregion

	#region Release & DEBUG
#if DEBUG
	public static bool DebugMode { get; } = true;
    public static bool EnableStartScreen = true;
	public static bool ForceLog { get; } = true;
#else
	public const bool DebugMode = false;
	private const bool EnableStartScreen = true;
	public const bool ForceLog = false;
#endif
	#endregion


	private bool isEnabled;
	public bool inited;

	#region Unity

	public void Awake()// Awake → OnEnable → Start
	{
		CommonUtils.Plugin.GUID = Plugin.GUID;
		CommonUtils.Plugin.NAME = Plugin.NAME;
		CommonUtils.Plugin.VERSION = Plugin.VERSION;
		CommonUtils.Plugin.Name = Plugin.Name;
		CommonUtils.Plugin.version = Plugin.version;
		CommonUtils.Plugin.buildTime = Plugin.buildTime;

		Log.LogDebug($"{Name} Mod Awake");
	}
	public void Start()
	{
		Log.LogDebug($"{Name} Mod Start");
	}
	public void Update()
	{
		CommonUtils.Plugin.plugin.Update();
	}

	#endregion

	public void OnEnable()
	{
		Log.LogDebug($"{Name} Mod OnEnable! isEnabled: {isEnabled}");

		if (this.isEnabled)
			return;
		this.isEnabled = true;

		CommonUtils.Plugin.plugin.OnEnable();

		// Put your custom hooks here!-在此放置你自己的钩子
		On.RainWorld.OnModsInit += On_RainWorld_OnModsInit;
		On.RainWorld.OnModsEnabled += On_RainWorld_OnModsEnabled;
		On.RainWorld.OnModsDisabled += On_RainWorld_OnModsDisabled;

		//PenetrationAbility.Hook();
		//FrameAbility.Hook();
		//ArcLightningAbility.Hook();
		Hooks.RegisterHooks();

		CommonUtils.Core.HookManager.Initialize();
	}

	public void OnDisable()
	{
		Log.LogDebug($"{Name} Mod OnDisable! isEnabled: {isEnabled}");

		if (!this.isEnabled)
			return;
		this.isEnabled = false;

		CommonUtils.Plugin.plugin.OnDisable();

		// Remove your custom hooks here!-在此取消你的钩子
		On.RainWorld.OnModsInit -= On_RainWorld_OnModsInit;
		On.RainWorld.OnModsEnabled -= On_RainWorld_OnModsEnabled;
		On.RainWorld.OnModsDisabled -= On_RainWorld_OnModsDisabled;

		CommonUtils.Core.HookManager.UnInitializeAll();
	}


	private void On_RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld rainWorld)
	{
		orig?.Invoke(rainWorld);

		Log.LogInfo($"{Name} Mod OnModsInit! inited: {inited}");

		try
		{
			RegisterOI();

			// Load any resources, such as sprites or sounds-加载任何资源 包括图像素材和音效

			// Put your custom hooks here!-在此放置你自己的钩子

		}
		catch (Exception ex)
		{
			Log.LogException(ex);
		}
	}

	private void On_RainWorld_OnModsEnabled(On.RainWorld.orig_OnModsEnabled orig, RainWorld rainWorld, ModManager.Mod[] newlyEnabledMods)
	{
		orig?.Invoke(rainWorld, newlyEnabledMods);

		Log.LogInfo($"{Name} Mod OnModsEnabled! inited: {inited}, newlyEnabledMods: {newlyEnabledMods}");

		if (this.inited)
			return;
		this.inited = true;


	}

	private void On_RainWorld_OnModsDisabled(On.RainWorld.orig_OnModsDisabled orig, RainWorld rainWorld, ModManager.Mod[] newlyDisabledMods)
	{
		orig?.Invoke(rainWorld, newlyDisabledMods);

		Log.LogInfo($"{Name} Mod OnModsDisabled! inited: {inited}, newlyDisabledMods: {newlyDisabledMods}");

		if (!this.inited)
			return;
		this.inited = false;

		try
		{
			// Remove your custom hooks here!-在此取消你的钩子


		}
		catch (Exception ex)
		{
			Log.LogException(ex);
		}
	}

	public static void RegisterOI()
	{
		try
		{
			if (MyOptions.Instance == null)
			{
				new MyOptions();
			}
			if (MachineConnector.GetRegisteredOI(GUID) != MyOptions.Instance)
			{
				MachineConnector.SetRegisteredOI(GUID, MyOptions.Instance);

				Log.LogDebug("Config interface registered successfully");
			}
			else
			{
				Log.LogWarning("Config interface registered failed");
			}
		}
		catch (Exception ex)
		{
			Log.LogError(Helper.Translate("Error registering option interface: ##").Replace("##", string.Format("{0}", ex)));
		}
	}
}
