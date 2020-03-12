using System;
using System.Linq;
using HarmonyLib;
using HugsLib;
using MapReroll.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace MapReroll.Compat {
	/// <summary>
	/// Compatibility tweaks to play nicely with Kiame's Configurable Maps
	/// </summary>
	public static class Compat_ConfigurableMaps {

		public static void Apply(Harmony harmonyInst) {
			try {
				if (GenTypes.GetTypeInAnyAssembly("ConfigurableMaps.HarmonyPatches") == null) return; // mod is not loaded
				Dialog_MapPreviews.DialogOnGUI -= ExtraMapPreviewsDialogOnGUI;
				Dialog_MapPreviews.DialogOnGUI += ExtraMapPreviewsDialogOnGUI;
				MapRerollController.Instance.Logger.Message("Applied Configurable Maps compatibility layer");
			} catch (Exception e) {
				MapRerollController.Instance.Logger.Error("Failed to apply compatibility layer for Configurable Maps:" + e);
			}
		}

		// add a button to the previews dialog for easy access to terrain settings
		private static void ExtraMapPreviewsDialogOnGUI(Dialog_MapPreviews previewsDialog, Rect inRect) {
			var closeButtonSize = new Vector2(120f, 40f);
			var configureButtonSize = new Vector2(140f, 40f);
			var elementSpacing = 10f;
			var buttonRect = new Rect(inRect.width - closeButtonSize.x - elementSpacing - configureButtonSize.x, inRect.yMax - configureButtonSize.y, configureButtonSize.x, configureButtonSize.y);
			if (Widgets.ButtonText(buttonRect, "Reroll2_previews_configureMap".Translate())) {
				var mod = LoadedModManager.ModHandles.FirstOrDefault(m => m.GetType().FullName == "ConfigurableMaps.Settings.TerrainSettingsController");
				if (mod != null) {
					previewsDialog.Close();
					var settingsDialog = new Dialog_ModSettings();
					// pre-select the terrain settings Mod
					ReflectionCache.DialogModSettings_SelMod.SetValue(settingsDialog, mod);
					Find.WindowStack.Add(settingsDialog);

					void TryReopenPreviews() {
						if (Find.WindowStack.IsOpen(settingsDialog)) {
							HugsLibController.Instance.DoLater.DoNextUpdate(TryReopenPreviews);
						} else {
							Find.WindowStack.Add(new Dialog_MapPreviews());
						}
					}
					TryReopenPreviews();
				}
			}
		}
	}
}