using HarmonyLib;
using RimWorld.Planet;

namespace MapReroll.Patches {
	/// <summary>
	/// Ensures that rerolled maps have the same caves/no caves setting as their original map
	/// This is needed since cave presence depends on world seed and we change it during generation
	/// </summary>
	[HarmonyPatch(typeof(World), "HasCaves", new []{typeof(int)})]
	internal static class World_HasCaves_Patch {
		[HarmonyPrefix]
		public static bool ConsistentRerollCaves(ref bool __result) {
			if (MapRerollController.HasCavesOverride.OverrideEnabled) {
				__result = MapRerollController.HasCavesOverride.HasCaves;
				return false;
			}
			return true;
		}
	}
}