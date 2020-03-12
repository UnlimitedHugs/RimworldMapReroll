using System;
using HarmonyLib;
using RimWorld;

namespace MapReroll.Patches {
	/// <summary>
	/// Ensures that things delivered by drop pod are registered to be carried over during a reroll
	/// </summary>
	[HarmonyPatch(typeof(ActiveDropPod), "PodOpen", new Type[0])]
	public class ActiveDropPod_PodOpen_Patch {
		[HarmonyPrefix]
		public static void RecordPodContents(ActiveDropPod __instance) {
			RerollToolbox.RecordPlayerAddedMapThings(__instance, __instance.Map);
		}
	}
}