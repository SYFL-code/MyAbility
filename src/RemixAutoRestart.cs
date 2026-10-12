using Menu;
using On.Menu;
using RWCustom;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

public class RemixAutoRestart : IUpdatable
{
	public static readonly RemixAutoRestart Instance = new RemixAutoRestart();

	public bool active => true;
	public int Priority => 1;


	public static uint steamAppId = 312520;
	public void Update()
	{
		if (CommonUtils.Plugin.DebugMode)
		{
			if (Input.GetKey(KeyCode.F5))
			{
				Restart();
			}
		}
	}

	public static void Restart()
	{
		try
		{
			string url = $"steam://rungameid/{steamAppId}";

			Process.Start(new ProcessStartInfo
			{
				FileName = "cmd.exe",
				Arguments = $"/c ping 127.0.0.1 -n 4 >nul && start \"\" \"{url}\"",
				CreateNoWindow = true,
				UseShellExecute = false
			});

			Application.Quit();
		}
		catch (Exception ex)
		{
			Log.LogError("Failed to restart via Steam. Please restart the game manually.");
			Log.LogError(ex);
		}
	}

}
