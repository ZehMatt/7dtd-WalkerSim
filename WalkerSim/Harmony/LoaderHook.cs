#if !DEDICATED_BUILD

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;

[HarmonyPatch(typeof(GameManager))]
[HarmonyPatch("StartGame")]
public class WalkerSimLoader : DMT.IHarmony
{
	public static WalkerSim.API api = null;
	
	public void Start()
	{
       Log.Out(" Loading Patch: " + GetType().ToString());
       var harmony = new Harmony(GetType().ToString());
       harmony.PatchAll(Assembly.GetExecutingAssembly());
	}
	
	static void Prefix()
	{
		Log.Out("[WalkerSim] Creating instance");
		api = new WalkerSim.API();
		api.InitMod();
	}
}

#endif