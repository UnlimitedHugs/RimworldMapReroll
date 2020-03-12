using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace MapReroll.Patches {
	/// <summary>
	/// A tool to apply multiple patches to make some map feature generators independent of their execution order
	/// </summary>
	public static class DeterministicGenerationPatcher {
		private const int DeterministicPatchPriority = 10;
		private static bool generatorSeedPushed;

		public static void InstrumentMethodForDeterministicGeneration(MethodInfo method, MethodInfo prefix, Harmony harmony) {
			if (method == null) {
				MapRerollController.Instance.Logger.Error($"Cannot instrument null method with prefix {prefix}: {Environment.StackTrace}");
				return;
			}
			harmony.Patch(method, 
				new HarmonyMethod(prefix) {priority = DeterministicPatchPriority},
				new HarmonyMethod(((Action)PopDeterministicRandState).Method) { priority = -DeterministicPatchPriority});
		}

		public static void DeterministicBeachSetup(Map map) {
			TryPushDeterministicRandState(map, 1);
		}

		public static void DeterministicPatchesSetup(Map map) {
			TryPushDeterministicRandState(map, 2);
		}

		public static void DeterministicRiverSetup(Map map) {
			TryPushDeterministicRandState(map, 3);
		}

		private static void TryPushDeterministicRandState(Map map, int seed) {
			if (MapRerollController.Instance.MapGeneratorModeSetting.Value == MapRerollController.MapGeneratorMode.AccuratePreviews) {
				var deterministicSeed = Gen.HashCombineInt(GenText.StableStringHash(Find.World.info.seedString + seed), map.Tile);
				Rand.PushState(deterministicSeed);
				generatorSeedPushed = true;
			}
		}

		private static void PopDeterministicRandState() {
			if (generatorSeedPushed) {
				generatorSeedPushed = false;
				Rand.PopState();
			}
		}
	}
}