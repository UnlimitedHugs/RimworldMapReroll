using Harmony;
using RimWorld;

namespace MapReroll.Patches {
	//private void PodOpen()
	[HarmonyPatch(typeof(ActiveDropPod), "PodOpen")]
	public class ActiveDropPod_PodOpen_Patch {
		[HarmonyPrefix]
		public static void RecordPodContents(ActiveDropPod __instance) {
			RerollToolbox.RecordPlayerAddedMapThings(__instance.Contents.ParentHolder, __instance.Map);
		}
	}
}