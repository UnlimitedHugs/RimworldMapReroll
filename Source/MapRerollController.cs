using System;
using System.Collections.Generic;
using System.Linq;
using HugsLib;
using HugsLib.Settings;
using HugsLib.Utils;
using MapReroll.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

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

		private readonly Queue<Action> scheduledMainThreadActions = new Queue<Action>();
		
		public override string ModIdentifier {
			get { return "MapReroll"; }
		}

		internal new ModLogger Logger {
			get { return base.Logger; }
		}

		private RerollWorldState _worldState;

		public RerollWorldState WorldState {
			get { return _worldState ?? (_worldState = UtilityWorldObjectManager.GetUtilityWorldObject<RerollWorldState>()); }
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
		public SettingHandle<int> WidgetSizeSetting { get; set; }
		public SettingHandle<bool> LoadingMessagesSetting { get; set; }

		// feel free to use these to detect reroll events 
		public event Action OnMapRerolled;
		public event Action OnGeysersRerolled;

		private readonly MapRerollUIController uiController;
		private MapGeneratorDef lastUsedMapGenerator;
		private GeyserRerollTool geyserReroll;
		private bool generatorSeedPushed;
		private bool pauseScheduled;
		private List<KeyValuePair<int, string>> cachedMapSizes;

		private MapRerollController() {
			Instance = this;
			uiController = new MapRerollUIController();
		}

		public override void Initialize() {
			ReflectionCache.PrepareReflection();
			PrepareSettingsHandles();
			RerollToolbox.LoadingMessages.UpdateAvailableLoadingMessageCount();
		}

		public override void MapComponentsInitializing(Map map) {
			if (Find.GameInitData != null) {
				WorldState.StartingTile = Find.GameInitData.startingTile;
			}
		}

		public override void MapGenerated(Map map) {
			RerollToolbox.StoreGeneratedThingIdsInMapState(map);
			var mapState = RerollToolbox.GetStateForMap(map);
			mapState.UsedMapGenerator = lastUsedMapGenerator;
		}

		public override void MapLoaded(Map map) {
			geyserReroll = new GeyserRerollTool();
			var mapState = RerollToolbox.GetStateForMap(map);
			if (!mapState.RerollGenerated || !PaidRerollsSetting) {
				mapState.ResourceBalance = MaxResourceBalance;
			}

			if (pauseScheduled) {
				pauseScheduled = false;
				ExecuteInMainThread(() => Find.TickManager.CurTimeSpeed = TimeSpeed.Paused);
			}

			RerollToolbox.TryStopPawnVomiting(map);

			if (mapState.RerollGenerated) {
				RerollToolbox.KillMapIntroDialog();
				if (PaidRerollsSetting) {
					// adjust map to current remaining resources and charge for the reroll
					RerollToolbox.ReduceMapResources(map, 100 - mapState.ResourceBalance, 100);
				}
				if (OnMapRerolled != null) OnMapRerolled();
			}

			uiController.MapLoaded(mapState.RerollGenerated);
		}

		public void RerollGeysers() {
			geyserReroll.DoReroll();
			if (PaidRerollsSetting) {
				RerollToolbox.SubtractResourcePercentage(Find.VisibleMap, Resources.Settings.MapRerollSettings.geyserRerollCost);
			}
			if (OnGeysersRerolled != null) OnGeysersRerolled();
		}

		public bool GeyserRerollInProgress {
			get { return geyserReroll.RerollInProgress; }
		}

		public override void Update() {
			if (geyserReroll != null) geyserReroll.OnUpdate();
			while (scheduledMainThreadActions.Count > 0) {
				scheduledMainThreadActions.Dequeue()();
			}
		}

		public override void OnGUI() {
			uiController.OnGUI();
		}

		public override void Tick(int currentTick) {
			if (geyserReroll != null) geyserReroll.OnTick();
		}
		
		public void RecordUsedMapGenerator(MapGeneratorDef def) {
			lastUsedMapGenerator = def;
		}

		public void TryPushDeterministicRandState(Map map, int seed) {
			if (MapGeneratorModeSetting.Value == MapGeneratorMode.AccuratePreviews) {
				var deterministicSeed = Gen.HashCombineInt(GenText.StableStringHash(Find.World.info.seedString+seed), map.Tile);
				Rand.PushState(deterministicSeed);
				generatorSeedPushed = true;
			}
		}

		public void TryPopDeterministicRandState() {
			if (generatorSeedPushed) {
				generatorSeedPushed = false;
				Rand.PopState();
			}
		}

		public void ExecuteInMainThread(Action action) {
			scheduledMainThreadActions.Enqueue(action);
		}

		public void PauseOnNextLoad() {
			pauseScheduled = true;
		}

		private void PrepareSettingsHandles() {
			SettingHandle.ShouldDisplay devModeVisible = () => Prefs.DevMode;

			PaidRerollsSetting = Settings.GetHandle("paidRerolls", "setting_paidRerolls_label".Translate(), "setting_paidRerolls_desc".Translate(), true);
			
			DeterministicRerollsSetting = Settings.GetHandle("deterministicRerolls", "setting_deterministicRerolls_label".Translate(), "setting_deterministicRerolls_desc".Translate(), true);

			AntiCheeseSetting = Settings.GetHandle("antiCheese", "setting_antiCheese_label".Translate(), "setting_antiCheese_desc".Translate(), true);
			AntiCheeseSetting.VisibilityPredicate = devModeVisible;

			LogConsumedResourcesSetting = Settings.GetHandle("logConsumption", "setting_logConsumption_label".Translate(), "setting_logConsumption_desc".Translate(), false);
			LogConsumedResourcesSetting.VisibilityPredicate = devModeVisible;

			NoVomitingSetting = Settings.GetHandle("noVomiting", "setting_noVomiting_label".Translate(), "setting_noVomiting_desc".Translate(), false);
			NoVomitingSetting.VisibilityPredicate = devModeVisible;

			LoadingMessagesSetting = Settings.GetHandle("loadingMessages", "setting_loadingMessages_label".Translate(), "setting_loadingMessages_desc".Translate(), true);

			WidgetSizeSetting = Settings.GetHandle("widgetSize", "setting_widgetSize_label".Translate(), "setting_widgetSize_desc".Translate(), MapRerollUIController.DefaultWidgetSize, Validators.IntRangeValidator(MapRerollUIController.MinWidgetSize, MapRerollUIController.MaxWidgetSize));
			WidgetSizeSetting.SpinnerIncrement = 8;

			MapGeneratorModeSetting = Settings.GetHandle("mapGeneratorMode", "setting_mapGeneratorMode_label".Translate(), "setting_mapGeneratorMode_desc".Translate(), MapGeneratorMode.AccuratePreviews, null, "setting_mapGeneratorMode_");

			var changeSize = Settings.GetHandle<bool>("changeMapSize", "setting_changeMapSize_label".Translate(), "setting_changeMapSize_desc".Translate());
			changeSize.Unsaved = true;
			changeSize.CustomDrawer = ChangeSizeCustomDrawer;
		}

		private bool ChangeSizeCustomDrawer(Rect rect) {
			var world = Current.Game != null ? Current.Game.World : null;
			if (world == null) {
				if (Widgets.ButtonText(rect, "setting_changeMapSize_noWorld".Translate())) {
					SoundDefOf.ClickReject.PlayOneShotOnCamera();
				}
			} else {
				var sizes = cachedMapSizes ?? (cachedMapSizes = RerollToolbox.GetAvailableMapSizes().Select(pair =>
					new KeyValuePair<int, string>(pair.Key, string.Format("{0}x{0}{1}", pair.Key, pair.Value != null ? " - " + pair.Value : null))
				).ToList());
				var currentIndex = sizes.FindIndex(p => p.Key == world.info.initialMapSize.x);
				if (currentIndex < 0) currentIndex = 0;
				if (Widgets.ButtonText(rect, sizes[currentIndex].Value)) {
					Find.WindowStack.Add(new FloatMenu(sizes.Select(p =>
						new FloatMenuOption { Label = p.Value, action = () => world.info.initialMapSize = new IntVec3(p.Key, 1, p.Key) }
					).ToList()));
				}
			}
			return false;
		}
	}
}