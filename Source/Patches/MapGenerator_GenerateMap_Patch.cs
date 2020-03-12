using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld.Planet;
using Verse;
using MapComponentUtility = Verse.MapComponentUtility;

namespace MapReroll.Patches {
	/// <summary>
	/// This is required to intercept and record the map generator used for the generation of a specific map.
	/// </summary>
	[HarmonyPatch(typeof(MapGenerator))]
	[HarmonyPatch("GenerateMap")]
	[HarmonyPatch(new[]{typeof(IntVec3), typeof(MapParent), typeof(MapGeneratorDef), typeof(IEnumerable<GenStepWithParams>), typeof(Action<Map>)})]
	internal static class MapGenerator_GenerateMap_Patch {
		private static bool patched;

		[HarmonyPrepare]
		public static void Prepare() {
			LongEventHandler.ExecuteWhenFinished(() => {
				if (!patched) MapRerollController.Instance.Logger.Error("MapGenerator_GenerateMap_Patch could not be applied.");
			});
		}

		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> RecordUsedMapGenerator(IEnumerable<CodeInstruction> instructions) {
			patched = false;
			var expectedMethod = AccessTools.Method(typeof(MapComponentUtility), "MapGenerated");
			if(expectedMethod == null) throw new Exception("Failed to reflect required method");
			foreach (var inst in instructions) {
				yield return inst;
				if (!patched && inst.opcode == OpCodes.Call && expectedMethod.Equals(inst.operand)) {
					// push Map
					yield return new CodeInstruction(OpCodes.Ldloc_2);
					// push MapGeneratorDef
					yield return new CodeInstruction(OpCodes.Ldarg_2);
					// call our method
					yield return new CodeInstruction(OpCodes.Call, ((Action<Map, MapGeneratorDef>)OnMapGenerated).Method);
					patched = true;
				}
			}
		}

		private static void OnMapGenerated(Map map, MapGeneratorDef def) {
			MapRerollController.Instance.OnMapGenerated(map, def);
		}
	}
}