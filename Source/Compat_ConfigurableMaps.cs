using System;
using System.Linq;
using Harmony;
using MapReroll.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace MapReroll.Compat {
	/// <summary>
	/// Compatibility tweaks to play nicely with Rainbeau's Configurable Maps
	/// </summary>
	public class Compat_ConfigurableMaps {
		private delegate TerrainDef RFRBeachMakerBeachTerrainAt(IntVec3 c, Map map, BiomeDef biome);

		private static RFRBeachMakerBeachTerrainAt BeachMakerBeachTerrainAt;

		public static void Apply(HarmonyInstance harmonyInst) {
			try {
				var rfrTerrainGenstep = GenTypes.GetTypeInAnyAssembly("RFR_Code.RFR_GenStep_Terrain");
				if (rfrTerrainGenstep == null) return; // mod is not loaded
				
				ReflectionCache.BeachMakerType = ReflectionCache.ReflectType("RFR_Code.RFR_BeachMaker", rfrTerrainGenstep.Assembly);
				if (ReflectionCache.BeachMakerType != null) {
					ReflectionCache.BeachMaker_Init = ReflectionCache.ReflectMethod("Init", ReflectionCache.BeachMakerType, typeof(void), new[] {typeof(Map)});
					ReflectionCache.BeachMaker_Cleanup = ReflectionCache.ReflectMethod("Cleanup", ReflectionCache.BeachMakerType, typeof(void), new Type[0]);
					var rfrBeachTerrainAt = ReflectionCache.ReflectMethod("BeachTerrainAt", ReflectionCache.BeachMakerType, typeof(TerrainDef), new[] {typeof(IntVec3), typeof(Map), typeof(BiomeDef)});
					BeachMakerBeachTerrainAt = (RFRBeachMakerBeachTerrainAt)Delegate.CreateDelegate(typeof(RFRBeachMakerBeachTerrainAt), null, rfrBeachTerrainAt);
					if (BeachMakerBeachTerrainAt == null) throw new Exception("Failed to create BeachTerrainAt delegate");
				}

				ReflectionCache.RiverMakerType = ReflectionCache.ReflectType("RFR_Code.RFR_RiverMaker");
				if (ReflectionCache.RiverMakerType != null) {
					ReflectionCache.GenStepTerrain_GenerateRiver = ReflectionCache.ReflectMethod("GenerateRiver", rfrTerrainGenstep, ReflectionCache.RiverMakerType, new[] {typeof(Map)});
					ReflectionCache.RiverMaker_TerrainAt = ReflectionCache.ReflectMethod("TerrainAt", ReflectionCache.RiverMakerType, typeof(TerrainDef), new[] {typeof(IntVec3), typeof(bool)});
				}

				// required on account of different signatures
				MapPreviewGenerator.AlternateBeachTerrainAtDelegate = AlternateBeachTerrainAt;

				// add button to access settings window
				Dialog_MapPreviews.DialogOnGUI -= ExtraMapPreviewsDialogOnGUI;
				Dialog_MapPreviews.DialogOnGUI += ExtraMapPreviewsDialogOnGUI;

				// ensures the modded BeachMaker is also affected by the "Map generator mode" setting
				var beachMakerInit = AccessTools.Method(AccessTools.TypeByName("RFR_Code.RFR_BeachMaker"), "Init");
				if (beachMakerInit == null) throw new Exception("Failed to reflect RFR_BeachMaker.Init");
				var prefix = ((Action<Map>)DeterministicBeachSetup).Method;
				var postfix = ((Action)DeterministicBeachTeardown).Method;
				harmonyInst.Patch(beachMakerInit, new HarmonyMethod(prefix), new HarmonyMethod(postfix));

				MapRerollController.Instance.Logger.Message("Applied Configurable Maps compatibility layer");
			} catch (Exception e) {
				MapRerollController.Instance.Logger.Error("Failed to apply compatibility layer for Configurable Maps:" +e);
			}
		}

		public static void DeterministicBeachSetup(Map map) {
			MapRerollController.Instance.TryPushDeterministicRandState(map, 1);
		}

		public static void DeterministicBeachTeardown() {
			MapRerollController.Instance.TryPopDeterministicRandState();
		}

		private static TerrainDef AlternateBeachTerrainAt(IntVec3 c, BiomeDef biome) {
			return BeachMakerBeachTerrainAt(c, null, biome);
		}

		// add a button to the previews dialog for easy access to terrain settings
		private static void ExtraMapPreviewsDialogOnGUI(Dialog_MapPreviews previewsDialog, Rect inRect) {
			var closeButtonSize = new Vector2(120f, 40f);
			var configureButtonSize = new Vector2(140f, 40f);
			var elementSpacing = 10f;
			if (Widgets.ButtonText(new Rect(inRect.width - closeButtonSize.x - elementSpacing - configureButtonSize.x, inRect.yMax - configureButtonSize.y, configureButtonSize.x, configureButtonSize.y), "Reroll2_previews_configureMap".Translate())) {
				var mod = LoadedModManager.ModHandles.FirstOrDefault(m => m.GetType().FullName == "RFR_Code.Controller_Terrain");
				if (mod != null) {
					previewsDialog.Close();
					var settingsDialog = new Dialog_ModSettings();
					// pre-select the terrain settings Mod
					ReflectionCache.DialogModSettings_SelMod.SetValue(settingsDialog, mod);
					Find.WindowStack.Add(settingsDialog);
				}
			}
		}
	}
}