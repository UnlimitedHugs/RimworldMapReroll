using System.Reflection;
using Harmony;
using Verse;

namespace MapReroll.Patches {
	[HarmonyPatch]
	internal class BeachMaker_Init_Patch {
		[HarmonyTargetMethod]
		public static MethodInfo GetMethod(HarmonyInstance inst) {
			return AccessTools.Method(AccessTools.TypeByName("BeachMaker"), "Init");
		}

		[HarmonyPrefix]
		public static void DeterministicBeachSetup(Map map) {
			MapRerollController.Instance.TryPushDeterministicRandState(map, 1);
		}

		[HarmonyPostfix]
		public static void DeterministicBeachTeardown() {
			MapRerollController.Instance.TryPopDeterministicRandState();
		}
	}
}