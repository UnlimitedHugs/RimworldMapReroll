using Harmony;
using RimWorld;
using Verse;

namespace MapReroll.Patches {
	[HarmonyPatch(typeof(TerrainPatchMaker), "Init")]
	internal class TerrainPatchMaker_Init_Patch {
		[HarmonyPrefix]
		public static void DeterministicPatchesSetup(Map map) {
			MapRerollController.Instance.TryPushDeterministicRandState(map, 2);
		}

		[HarmonyPostfix]
		public static void DeterministicPatchesTeardown() {
			MapRerollController.Instance.TryPopDeterministicRandState();
		}
	}
}