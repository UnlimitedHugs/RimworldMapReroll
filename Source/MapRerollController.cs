// ReSharper disable EventNeverSubscribedTo.Global
using System;
using HugsLib;
using HugsLib.Settings;
using HugsLib.Utils;
using Verse;

namespace MapReroll {
	/// <summary>
	/// The hub of the mod. Instantiated by HugsLib.
	/// </summary>
	public class MapRerollController : ModBase {
		internal const int DefaultMapSize = 250*250;
		internal const int LargestMapSize = 400*400;

		public enum MapRerollType {
			Map, Geyser
		}

		public override string ModIdentifier {
			get { return "MapReroll"; }
		}

		private static MapRerollController _instance;
		public static MapRerollController Instance {
			get {
				if (_instance == null) throw new NullReferenceException("MapRerollController as not been initialized by HugsLib");
				return _instance;
			}
		}

		// feel free to use these to detect reroll events 
		public event Action OnMapRerolled;
		public event Action OnGeysersRerolled;

		internal new ModLogger Logger {
			get { return base.Logger; }
		}
		
		public MapRerollState RerollState {
			get {
				var comp = TryGetStateComponentFromCurrentMap();
				return comp == null ? null : comp.State;
			}
			private set {
				var comp = TryGetStateComponentFromCurrentMap();
				if (comp == null) throw new Exception("Could not set MapRerollState- no visible map or state map component");
				comp.State = value;
			}
		}

		private MapRerollSettings _settingHandles;
		internal MapRerollSettings SettingHandles {
			get {
				if(_settingHandles == null) throw new Exception("Setting handles have not been initialized yet");
				return _settingHandles;
			}
			private set { _settingHandles = value; }
		}

		internal bool RerollInProgress { get; private set; }

		private readonly MapRerollUIController uiController;
		private MapComponent_MapRerollState lastReturnedMapComponent;
		private MapRerollState stateFromLastMap;

		private MapRerollController() {
			_instance = this;
			uiController = new MapRerollUIController();
		}

		public override void Initialize() {
			ReflectionCache.PrepareReflection();
		}

		public override void DefsLoaded() {
			MapRerollToolbox.LoadingMessages.UpdateAvailableLoadingMessageCount();
			PrepareSettingsHandles();
		}

		public override void MapComponentsInitializing(Map map) {
			var stateComp = GetStateComponentFromMap(map); // since VisibleMap is not set yet, this is needed to access RerollState
			var initData = Current.Game.InitData;
			if (Scribe.mode == LoadSaveMode.LoadingVars) {
				// loading a save. InitData could still be set from previous map generation
				initData = null;
			}
			if (stateFromLastMap != null) {
				// state is stored in the map, so the last one was lost. Restore it in the newly generated map.
				stateComp.State = stateFromLastMap;
				stateFromLastMap = null;
			}
			if (initData != null && !RerollInProgress) {
				// this might be a post-reroll load, so we take only the MapInitData info
				var state = stateComp.State;
				if (state == null) stateComp.State = state = new MapRerollState();
				state.ResourcesPercentBalance = 100f;
				state.OriginalWorldSeed = Find.World.info.seedString;
				state.LastRerollSeed = state.OriginalWorldSeed;
				state.HasInitData = true;
				state.StartingTile = initData.startingTile;
				state.MapSize = initData.mapSize;
				state.StartingSeason = initData.startingSeason;
				state.Permadeath = initData.permadeath;
			}
		}

		public override void MapLoaded(Map map) {
			if (RerollState == null) return; // mod was added to an existing map
			RerollState.UsedMapGenerator = MapRerollToolbox.TryGetMostLikelyMapGenerator(); // temporary fix until the generator capturing patch issue is figured out (inb4 forever solution)
			var noInitData = !RerollState.HasInitData;
			var noMapGenerator = RerollState.UsedMapGenerator == null;
			if (noInitData || noMapGenerator) {
				if (noInitData) {
					Logger.Error("MapRerollState found, but state has no map init data: " + RerollState);
				}
				if (noMapGenerator) {
					Logger.Warning("Map generator could not be captured: " + RerollState);
				}
				RerollState = null;
				return;
			}
			
			MapRerollToolbox.ResetScattererGensteps(RerollState.UsedMapGenerator);
			MapRerollToolbox.TryStopStartingPawnVomiting(map);

			if (!SettingHandles.PaidRerolls) {
				RerollState.ResourcesPercentBalance = 100f;
			}
			if(RerollInProgress) {
				if (SettingHandles.PaidRerolls) {
					// adjust map to current remaining resources and charge for the reroll
					MapRerollToolbox.ReduceMapResources(map, 100 - (RerollState.ResourcesPercentBalance), 100);
					MapRerollToolbox.SubtractResourcePercentage(map, MapRerollDefOf.MapRerollSettings.mapRerollCost, RerollState);
				}
				MapRerollToolbox.RecordPodLandingTaleForColonists(MapRerollToolbox.GetAllColonistsOnMap(map), Find.Scenario);
				Find.World.info.seedString = RerollState.OriginalWorldSeed;
				MapRerollToolbox.KillMapIntroDialog();
				MapRerollToolbox.LoadingMessages.RestoreVanillaLoadingMessage();
				if(OnMapRerolled!=null) OnMapRerolled();
			}
			uiController.MapLoaded(RerollInProgress);
			RerollInProgress = false;
		}

		public override void OnGUI() {
			uiController.OnGUI();
		}

		public AcceptanceReport CanRerollMap() {
			const string lockedStringKey = "MapReroll_rerollLocked";
			var state = RerollState;
			if (Find.VisibleMap == null || state == null) 
				return new AcceptanceReport(lockedStringKey.Translate("MapReroll_rerollLocked_starting".Translate()));
			if ((bool)ReflectionCache.MapParent_AnyCaravanEverFormed.GetValue(Find.WorldObjects.FactionBaseAt(Find.VisibleMap.Tile))) 
				return new AcceptanceReport(lockedStringKey.Translate("MapReroll_rerollLocked_caravan".Translate()));
			if (MapRerollToolbox.GetAllColonistsOnMap(Find.VisibleMap).Count == 0) 
				return new AcceptanceReport(lockedStringKey.Translate("MapReroll_rerollLocked_colonists".Translate()));
			if (!CanAffordOperation(MapRerollType.Map)) 
				return new AcceptanceReport(lockedStringKey.Translate("MapReroll_rerollLocked_balance".Translate()));
			return true;
		}

		public bool CanAffordOperation(MapRerollType type) {
			EnsureStateInfoExists();
			float cost = 0;
			switch (type) {
				case MapRerollType.Map: cost = MapRerollDefOf.MapRerollSettings.mapRerollCost; break;
				case MapRerollType.Geyser: cost = MapRerollDefOf.MapRerollSettings.geyserRerollCost; break;
			}
			return !SettingHandles.PaidRerolls || RerollState.ResourcesPercentBalance >= cost;
		}

		public void RerollGeysers() {
			EnsureStateInfoExists();
			try {
				var map = Find.VisibleMap;
				if (map == null) throw new Exception("Must use on a visible map");
				var geyserGen = MapRerollToolbox.TryGetGeyserGenstep(RerollState.UsedMapGenerator);
				if (geyserGen != null) {
					MapRerollToolbox.TryGenerateGeysersWithNewLocations(map, geyserGen);
					if (SettingHandles.PaidRerolls) {
						MapRerollToolbox.SubtractResourcePercentage(map, MapRerollDefOf.MapRerollSettings.geyserRerollCost, RerollState);
					}
					if (OnGeysersRerolled != null) OnGeysersRerolled();
				} else {
					Logger.Error("Failed to find the Genstep for geysers. Check your map generator config.");
				}
			} catch (Exception e) {
				Logger.ReportException(e);
			}
		}

		public void RerollMap() {
			EnsureStateInfoExists();
			if (RerollInProgress) return;
			try {
				if (!CanRerollMap().Accepted) return;
				var map = Find.VisibleMap;
				var state = RerollState;
				stateFromLastMap = state;
				if (map == null) throw new Exception("Must use on a visible map");
				if(!state.HasInitData) throw new Exception("No MapInitData was captured. Trying to reroll a loaded map?");
				RerollInProgress = true;

				var colonists = MapRerollToolbox.GetAllColonistsOnMap(map);
				MapRerollToolbox.PrepareColonistsForReroll(colonists);

				Find.Selector.SelectedObjects.Clear();

				var sameWorld = Current.Game.World;
				var sameScenario = Current.Game.Scenario;
				var sameStoryteller = Current.Game.storyteller;

				MapRerollToolbox.ClearShrineCasketOwnersFromWorld(map, sameWorld);

				MapRerollToolbox.DiscardWorldTileFaction(state.StartingTile, sameWorld);
				
				Current.ProgramState = ProgramState.Entry;
				Current.Game = new Game();
				var newInitData = Current.Game.InitData = new GameInitData();
				Current.Game.Scenario = sameScenario;
				
				MapRerollToolbox.ResetIncidentScenarioParts(sameScenario);
				Compat_CrashLanding.TryReplaceHardCrashLandingPawnStart(sameScenario);
				
				Find.Scenario.PreConfigure();

				newInitData.permadeath = state.Permadeath;
				newInitData.startingTile = state.StartingTile;
				newInitData.startingSeason = state.StartingSeason;
				newInitData.mapSize = state.MapSize;

				Current.Game.World = sameWorld;
				Current.Game.storyteller = sameStoryteller;
				state.LastRerollSeed = sameWorld.info.seedString = MapRerollToolbox.GenerateNewRerollSeed(state.LastRerollSeed);

				Find.Scenario.PostWorldGenerate();
				newInitData.PrepForMapGen();
				Find.Scenario.PreMapGenerate();

				// trash all newly generated pawns
				StartingPawnUtility.ClearAllStartingPawns();

				newInitData.startingPawns = colonists;
				foreach (var startingPawn in newInitData.startingPawns) {
					startingPawn.SetFactionDirect(newInitData.playerFaction);
				}

				MapRerollToolbox.LoadingMessages.SetCustomLoadingMessage(SettingHandles.LoadingMessages);
				LongEventHandler.QueueLongEvent(null, "Play", "GeneratingMap", true, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
			} catch (Exception e) {
				Logger.ReportException(e);
			}
		}

		private void PrepareSettingsHandles() {
			var handles = SettingHandles = new MapRerollSettings();
			
			handles.PaidRerolls = Settings.GetHandle("paidRerolls", "setting_paidRerolls_label".Translate(), "setting_paidRerolls_desc".Translate(), true);
			
			handles.LoadingMessages = Settings.GetHandle("loadingMessages", "setting_loadingMessages_label".Translate(), "setting_loadingMessages_desc".Translate(), true);

			handles.WidgetSize = Settings.GetHandle("widgetSize", "setting_widgetSize_label".Translate(), "setting_widgetSize_desc".Translate(), MapRerollUIController.DefaultWidgetSize, Validators.IntRangeValidator(MapRerollUIController.MinWidgetSize, MapRerollUIController.MaxWidgetSize));
			handles.WidgetSize.SpinnerIncrement = 8;
			
			SettingHandle.ShouldDisplay devModeVisible = () => Prefs.DevMode;

			handles.LogConsumedResources = Settings.GetHandle("logConsumption", "setting_logConsumption_label".Translate(), "setting_logConsumption_desc".Translate(), false);
			handles.LogConsumedResources.VisibilityPredicate = devModeVisible;

			handles.NoVomiting = Settings.GetHandle("noVomiting", "setting_noVomiting_label".Translate(), "setting_noVomiting_desc".Translate(), false);
			handles.NoVomiting.VisibilityPredicate = devModeVisible;

			handles.LogConsumedResources.VisibilityPredicate = devModeVisible;
			handles.SecretFound = Settings.GetHandle("secretFound", "", null, false);
			handles.SecretFound.NeverVisible = true;
		}

		private MapComponent_MapRerollState TryGetStateComponentFromCurrentMap() {
			var map = Find.VisibleMap;
			if (map == null) return null;
			if (lastReturnedMapComponent != null && lastReturnedMapComponent.map == map) { // cache component for faster access
				return lastReturnedMapComponent;
			}
			lastReturnedMapComponent = GetStateComponentFromMap(map);
			return lastReturnedMapComponent;
		}

		private MapComponent_MapRerollState GetStateComponentFromMap(Map map) {
			if (map == null) throw new NullReferenceException("map is null");
			var comp = map.GetComponent<MapComponent_MapRerollState>();
			if (comp == null) throw new NullReferenceException("Map does not have expected MapComponent_MapRerollState");
			return comp;
		}

		private void EnsureStateInfoExists() {
			if (RerollState == null) throw new Exception("Current map has no reroll state information");
		}

		public class MapRerollSettings {
			public SettingHandle<bool> PaidRerolls { get; set; }
			public SettingHandle<bool> LoadingMessages { get; set; }
			public SettingHandle<int> WidgetSize { get; set; }
			public SettingHandle<bool> LogConsumedResources { get; set; }
			public SettingHandle<bool> NoVomiting { get; set; }
			public SettingHandle<bool> SecretFound { get; set; }
		}
	}
}