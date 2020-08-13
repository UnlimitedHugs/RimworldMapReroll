using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HugsLib;
using HugsLib.Settings;
using HugsLib.Utils;
using MapReroll.Compat;
using MapReroll.Patches;
using MapReroll.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace MapReroll {
	/// <summary>
	/// The hub of the mod. Instantiated by HugsLib.
	/// </summary>
	public class MapRerollController : ModBase {
		internal const float MaxResourceBalance = 100f;
		
		public enum MapGeneratorMode {
			AccuratePreviews, OriginalGenerator
		}

		public static MapRerollController Instance { get; private set; }

		// prevents the main thread from messing with the Rand seeding done in the preview thread
		public static bool RandStateStackCheckingPaused { get; set; }
		// ensures cave presence is consistent across rerolls
		public static readonly HasCavesOverride HasCavesOverride = new HasCavesOverride();

		public override string ModIdentifier {
			get { return "MapReroll"; }
		}

		internal new ModLogger Logger {
			get { return base.Logger; }
		}

		private RerollWorldState _worldState;

		public RerollWorldState WorldState {
			get { return _worldState ?? (_worldState = Find.World.GetComponent<RerollWorldState>()); }
		}

		public MapRerollUIController UIController {
			get { return uiController; }
		}

		public SettingHandle<bool> PaidRerollsSetting { get; private set; }
		public SettingHandle<bool> DeterministicRerollsSetting { get; private set; }
		public SettingHandle<bool> AntiCheeseSetting { get; private set; }
		public SettingHandle<bool> LogConsumedResourcesSetting { get; private set; }
		public SettingHandle<bool> NoVomitingSetting { get; private set; }
		public SettingHandle<MapGeneratorMode> MapGeneratorModeSetting { get; set; }
		public SettingHandle<int> WidgetSizeSetting { get; private set; }
		public SettingHandle<bool> LoadingMessagesSetting { get; private set; }
		public SettingHandle<bool> GeyserArrowsSetting { get; private set; }
		public SettingHandle<bool> PreviewCavesSetting { get; private set; }

		// feel free to use these to detect reroll events
		// ReSharper disable EventNeverSubscribedTo.Global
		public event Action OnMapRerolled;
		public event Action OnGeysersRerolled;

		private readonly MapRerollUIController uiController;
		private GeyserRerollTool geyserReroll;
		private bool pauseScheduled;
		private List<(int size, string btnLabel, string optionLabel)> cachedMapSizeLabels;
		private bool rerollInProgress;

		private MapRerollController() {
			Instance = this;
			uiController = new MapRerollUIController();
		}

		public override void Initialize() {
			ApplyDeterministicGenerationPatches();
			ReflectionCache.PrepareReflection();
			PrepareSettingsHandles();
			RerollToolbox.LoadingMessages.UpdateAvailableLoadingMessageCount();
			Compat_ConfigurableMaps.Apply(HarmonyInst);
			Compat_PrepareCarefully.Apply(HarmonyInst);
		}

		private void ApplyDeterministicGenerationPatches() {
			DeterministicGenerationPatcher.InstrumentMethodForDeterministicGeneration(
				AccessTools.Method(GenTypes.GetTypeInAnyAssembly("Rimworld.BeachMaker"), "Init"),
				((Action<Map>)DeterministicGenerationPatcher.DeterministicBeachSetup).Method, HarmonyInst);
			DeterministicGenerationPatcher.InstrumentMethodForDeterministicGeneration(
				AccessTools.Method(typeof(TerrainPatchMaker), "Init"),
				((Action<Map>)DeterministicGenerationPatcher.DeterministicPatchesSetup).Method, HarmonyInst);
			DeterministicGenerationPatcher.InstrumentMethodForDeterministicGeneration(
				AccessTools.Method(typeof(GenStep_Terrain), "GenerateRiver"),
				((Action<Map>)DeterministicGenerationPatcher.DeterministicRiverSetup).Method, HarmonyInst);
		}

		public override void MapComponentsInitializing(Map map) {
			if (Find.GameInitData != null) {
				WorldState.StartingTile = Find.GameInitData.startingTile;
			}
		}

		public override void MapLoaded(Map map) {
			geyserReroll = new GeyserRerollTool();
			if (rerollInProgress) {
				RerollToolbox.KillMapIntroDialog();
				if (OnMapRerolled != null) OnMapRerolled();
			}

			if (pauseScheduled) {
				pauseScheduled = false;
				HugsLibController.Instance.DoLater.DoNextUpdate(() => Find.TickManager.CurTimeSpeed = TimeSpeed.Paused);
			}

			RerollToolbox.TryStopPawnVomiting(map);
			uiController.MapLoaded(rerollInProgress);
			rerollInProgress = false;
		}

		public void RerollMap(string seed) {
			rerollInProgress = true;
			RerollToolbox.DoMapReroll(seed);
		}

		public void RerollGeysers() {
			geyserReroll.DoReroll();
			if (PaidRerollsSetting) {
				RerollToolbox.SubtractResourcePercentage(Find.CurrentMap, Resources.Settings.MapRerollSettings.geyserRerollCost);
			}
			if (OnGeysersRerolled != null) OnGeysersRerolled();
		}

		public bool GeyserRerollInProgress {
			get { return geyserReroll.RerollInProgress; }
		}

		public override void Update() {
			if (geyserReroll != null) geyserReroll.OnUpdate();
		}

		public override void Tick(int currentTick) {
			if(geyserReroll != null) geyserReroll.OnTick();
		}

		public override void OnGUI() {
			uiController.OnGUI();
		}

		internal void PauseOnNextLoad() {
			pauseScheduled = true;
		}

		internal void OnMapGenerated(Map map, MapGeneratorDef usedMapGenerator) {
			RerollToolbox.StoreGeneratedThingIdsInMapState(map);
			var mapState = RerollToolbox.GetStateForMap(map);
			mapState.UsedMapGenerator = usedMapGenerator;
			if (!rerollInProgress) {
				mapState.ResourceBalance = MaxResourceBalance;
			}
		}

		private void PrepareSettingsHandles() {
			bool DevModeVisible() => Prefs.DevMode;

			PaidRerollsSetting = Settings.GetHandle("paidRerolls", "setting_paidRerolls_label".Translate(), "setting_paidRerolls_desc".Translate(), true);
			
			DeterministicRerollsSetting = Settings.GetHandle("deterministicRerolls", "setting_deterministicRerolls_label".Translate(), "setting_deterministicRerolls_desc".Translate(), true);

			AntiCheeseSetting = Settings.GetHandle("antiCheese", "setting_antiCheese_label".Translate(), "setting_antiCheese_desc".Translate(), true);
			AntiCheeseSetting.VisibilityPredicate = DevModeVisible;

			LogConsumedResourcesSetting = Settings.GetHandle("logConsumption", "setting_logConsumption_label".Translate(), "setting_logConsumption_desc".Translate(), false);
			LogConsumedResourcesSetting.VisibilityPredicate = DevModeVisible;

			NoVomitingSetting = Settings.GetHandle("noVomiting", "setting_noVomiting_label".Translate(), "setting_noVomiting_desc".Translate(), false);
			NoVomitingSetting.VisibilityPredicate = DevModeVisible;

			LoadingMessagesSetting = Settings.GetHandle("loadingMessages", "setting_loadingMessages_label".Translate(), "setting_loadingMessages_desc".Translate(), true);

			GeyserArrowsSetting = Settings.GetHandle("geyserArrows", "setting_geyserArrows_label".Translate(), "setting_geyserArrows_desc".Translate(), true);

			PreviewCavesSetting = Settings.GetHandle("previewCaves", "setting_previewCaves_label".Translate(), "setting_previewCaves_desc".Translate(), true);

			WidgetSizeSetting = Settings.GetHandle("widgetSize", "setting_widgetSize_label".Translate(), "setting_widgetSize_desc".Translate(), MapRerollUIController.DefaultWidgetSize, Validators.IntRangeValidator(MapRerollUIController.MinWidgetSize, MapRerollUIController.MaxWidgetSize));
			WidgetSizeSetting.SpinnerIncrement = 8;

			MapGeneratorModeSetting = Settings.GetHandle("mapGeneratorMode", "setting_mapGeneratorMode_label".Translate(), "setting_mapGeneratorMode_desc".Translate(), MapGeneratorMode.AccuratePreviews, null, "setting_mapGeneratorMode_");

			var mapActions = Settings.GetHandle<bool>("mapActions", "setting_mapActions_label".Translate(), "setting_mapActions_desc".Translate());
			mapActions.Unsaved = true;
			mapActions.CustomDrawerHeight = 64f;
			mapActions.CustomDrawer = MapActionsCustomDrawer;
		}

		private bool MapActionsCustomDrawer(Rect rect) {
			DrawMapSizeButton(rect.TopHalf(), "setting_changeMapSize_btn", "setting_changeMapSize_desc");
			DrawReenableRerollsButton(rect.BottomHalf(), "setting_reenableRerolls_btn", "setting_reenableRerolls_desc");
			return false;

			void DrawMapSizeButton(Rect btnRect, string labelKey, string tooltipKey) {
				var world = Current.Game != null ? Current.Game.World : null;
				var sizes = cachedMapSizeLabels ?? (cachedMapSizeLabels = PrepareMapSizeLabels());
				var buttonActive = world != null;
				string buttonLabel;
				if (buttonActive) {
					var currentIndex = sizes.FindIndex(p => p.size == world.info.initialMapSize.x);
					if (currentIndex < 0) currentIndex = 0;
					buttonLabel = sizes[currentIndex].btnLabel;
				} else {
					buttonLabel = labelKey.Translate("");
				}
				if (MapRerollUtility.DrawActiveButton(btnRect, buttonLabel, tooltipKey, buttonActive) && buttonActive) {
					Find.WindowStack.Add(new FloatMenu(sizes.Select(p =>
						new FloatMenuOption(p.optionLabel,
							() => world.info.initialMapSize = new IntVec3(p.size, 1, p.size))
					).ToList()));
				}
				TooltipHandler.TipRegion(btnRect, tooltipKey.Translate());

				List<(int size, string btnLabel, string optionLabel)> PrepareMapSizeLabels() {
					return RerollToolbox.GetAvailableMapSizes().Select(pair =>
						(size: pair.Key, btnLabel: labelKey.Translate(pair.Key).RawText,
							optionLabel: $"{pair.Key}x{pair.Key}{(pair.Value != null ? " - " + pair.Value : null)}")
					).ToList();
				}
			}

			void DrawReenableRerollsButton(Rect btnRect, string labelKey, string tooltipKey) {
				var mapState = Find.CurrentMap?.GetComponent<MapComponent_MapRerollState>()?.State;
				var buttonActive = mapState != null && mapState.MapCommitted;
				if (MapRerollUtility.DrawActiveButton(btnRect, labelKey.Translate(), tooltipKey, buttonActive)
					&& buttonActive) {
					mapState.MapCommitted = false;
					uiController.ResetCache();
				}
				TooltipHandler.TipRegion(btnRect, tooltipKey.Translate());
			}
		}
	}

	public class HasCavesOverride {
		public bool OverrideEnabled { get; set; }
		public bool HasCaves { get; set; }
	}
}