using System;
using HugsLib;
using HugsLib.Settings;
using HugsLib.Utils;
using Verse;

namespace MapReroll {
	public class MapRerollController : ModBase {
		internal const float DefaultMapSize = 250*250;
		internal const float LargestMapSize = 400*400;

		public enum MapRerollType {
			Map, Geyser
		}

		public override string ModIdentifier {
			get { return "MapReroll"; }
		}

		private static MapRerollController _instance;
		public static MapRerollController Instance {
			get { return _instance; }
		}

		// feel free to use these to detect reroll events 
		// ReSharper disable EventNeverSubscribedTo.Global
		public event Action OnMapRerolled;
		public event Action OnGeysersRerolled;

		internal new ModLogger Logger {
			get { return base.Logger; }
		}

		private MapRerollState _state;
		public MapRerollState RerollState {
			get { return _state ?? (_state = new MapRerollState()); }
		}
		public class MapRerollState {
			// % of resources remaining as currency for rerolls
			public float ResourcesPercentBalance;
			// the world seed before any rerolls
			public string OriginalWorldSeed;
			// the world seed of the last performed reroll
			public string LastRerollSeed;
			// the MapInitData the player initially started with
			public GameInitData InitData;
		}

		private MapRerollSettings _settingHandles;
		internal MapRerollSettings SettingHandles {
			get {
				if(_settingHandles == null) throw new Exception("Setting handles have not been initialized yet");
				return _settingHandles;
			}
			private set { _settingHandles = value; }
		}

		internal bool RerollInProgress {
			get { return mapRerollTriggered; }
		}

		private readonly MapRerollUIController uiController;
		private bool mapRerollTriggered;

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
			RerollState.InitData = Current.Game.InitData;
			if (Scribe.mode == LoadSaveMode.LoadingVars) {
				// loading a save. InitData could still be set from previous map generation
				RerollState.InitData = null;
			}
		}

		public override void MapLoaded(Map map) {
			if(!ModIsActive) return;
			if(RerollState.InitData == null) return; // map was loaded from save
			
			MapRerollToolbox.ResetScattererGensteps(MapRerollToolbox.TryGetMostLikelyMapGenerator());
			MapRerollToolbox.TryStopStartingPawnVomiting(RerollState);

			if (!SettingHandles.PaidRerolls) {
				RerollState.ResourcesPercentBalance = 100f;
			}
			if(mapRerollTriggered) {
				if (SettingHandles.PaidRerolls) {
					// adjust map to current remaining resources and charge for the reroll
					MapRerollToolbox.ReduceMapResources(map, 100 - (RerollState.ResourcesPercentBalance), 100);
					MapRerollToolbox.SubtractResourcePercentage(map, MapRerollDefOf.MapRerollSettings.mapRerollCost, RerollState);
				}
				MapRerollToolbox.RecordPodLandingTaleForColonists(RerollState.InitData.startingPawns, Find.Scenario);
				Find.World.info.seedString = RerollState.OriginalWorldSeed;
				MapRerollToolbox.KillMapIntroDialog();
				MapRerollToolbox.LoadingMessages.RestoreVanillaLoadingMessage();
				if(OnMapRerolled!=null) OnMapRerolled();
			} else {
				RerollState.ResourcesPercentBalance = 100f;
				RerollState.OriginalWorldSeed = RerollState.LastRerollSeed = Find.World.info.seedString;
			}
			uiController.MapLoaded(mapRerollTriggered);
			mapRerollTriggered = false;
		}

		public override void OnGUI() {
			uiController.OnGUI();
		}

		public bool CanAffordOperation(MapRerollType type) {
			float cost = 0;
			switch (type) {
				case MapRerollType.Map: cost = MapRerollDefOf.MapRerollSettings.mapRerollCost; break;
				case MapRerollType.Geyser: cost = MapRerollDefOf.MapRerollSettings.geyserRerollCost; break;
			}
			return !SettingHandles.PaidRerolls || RerollState.ResourcesPercentBalance >= cost;
		}

		public void RerollGeysers() {
			try {
				var map = Find.VisibleMap;
				if (map == null) throw new Exception("Must use on a visible map");
				var geyserGen = MapRerollToolbox.TryGetGeyserGenstep();
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
			if (mapRerollTriggered) return;
			try {
				var map = Find.VisibleMap;
				if (map == null) throw new Exception("Must use on a visible map");
				if(RerollState.InitData == null) throw new Exception("No MapInitData was captured. Trying to reroll a loaded map?");
				mapRerollTriggered = true;

				var colonists = MapRerollToolbox.GetAllColonistsOnMap(map);
				MapRerollToolbox.PrepareColonistsForReroll(colonists);

				Find.Selector.SelectedObjects.Clear();

				var sameWorld = Current.Game.World;
				var sameScenario = Current.Game.Scenario;
				var sameStoryteller = Current.Game.storyteller;

				MapRerollToolbox.ClearShrineCasketOwnersFromWorld(map, sameWorld);

				MapRerollToolbox.DiscardWorldTileFaction(RerollState.InitData.startingTile, sameWorld);
				
				Current.ProgramState = ProgramState.Entry;
				Current.Game = new Game();
				var newInitData = Current.Game.InitData = new GameInitData();
				Current.Game.Scenario = sameScenario;
				
				MapRerollToolbox.ResetIncidentScenarioParts(sameScenario);
				Compat_CrashLanding.TryReplaceHardCrashLandingPawnStart(sameScenario);
				
				Find.Scenario.PreConfigure();

				newInitData.permadeath = RerollState.InitData.permadeath;
				newInitData.startingTile = RerollState.InitData.startingTile;
				newInitData.startingSeason = RerollState.InitData.startingSeason;
				newInitData.mapSize = RerollState.InitData.mapSize;

				Current.Game.World = sameWorld;
				Current.Game.storyteller = sameStoryteller;
				RerollState.LastRerollSeed = sameWorld.info.seedString = MapRerollToolbox.GenerateNewRerollSeed(RerollState.LastRerollSeed);

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