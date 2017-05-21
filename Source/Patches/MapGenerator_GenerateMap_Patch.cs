using Harmony;
using Verse;

namespace MapReroll.Patches {
	/// <summary>
	/// Stores the MapGeneratorDef so that we may use the same map generator when resetting GenStep state and rerolling geysers
	/// </summary>
	[HarmonyPatch(typeof(MapGenerator), "GenerateMap")]
	internal class MapGenerator_GenerateMap_Patch {
		[HarmonyPostfix]
		public static void RememberUsedMapGenerator(Map __result, MapGeneratorDef mapGenerator) {
			MapRerollController.Instance.ReportUsedMapGenerator(__result, mapGenerator);
		} 
	}
}