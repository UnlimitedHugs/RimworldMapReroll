using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HugsLib;
using HugsLib.Settings;
using RimWorld;
using UnityEngine;
using Verse;
using Random = UnityEngine.Random;

namespace MapReroll {
	public class MapRerollController : ModBase {
		private const int DefaultWidgetSize = 48;
		private const int MinWidgetSize = 16;
		private const int MaxWidgetSize = 64;
		private const float DefaultMapSize = 250*250;
		private const float LargestMapSize = 400*400;

		public enum MapRerollType {
			Map, Geyser
		}

		private static MapRerollController instance;
		public static MapRerollController Instance {
			get {
				return instance ?? (instance = new MapRerollController());
			}
		}

		private const string LoadingMessageKey = "GeneratingMap";
		private const string CustomLoadingMessagePrefix = "MapReroll_loading";

		// feel free to use these to detect reroll events 
		// ReSharper disable EventNeverSubscribedTo.Global
		public event Action OnMapRerolled;
		public event Action OnGeysersRerolled;

		public override string ModIdentifier {
			get { return "MapReroll"; }
		}

		public MapRerollDef SettingsDef {
			get { return MapRerollDefOf.MapRerollSettings; }
		}

		public bool ShowInterface {
			get { return Current.ProgramState == ProgramState.MapPlaying && !mapRerollTriggered && SettingsDef.enableInterface && Current.Game != null && Current.Game.Map != null && capturedInitData != null && !Faction.OfPlayer.HasName; }
		}

		public float WidgetSize {
			get { return settingWidgetSize.Value; }
		}

		public bool PaidRerolls {
			get { return settingPaidRerolls.Value; }
		}

		internal new ModLogger Logger {
			get { return base.Logger; }
		}

		public float ResourcePercentageRemaining { get; private set; }
		
		private readonly RerollGUIWidget guiWidget;

		private FieldInfo thingPrivateStateField;
		private FieldInfo genstepScattererProtectedUsedSpots;
		private FieldInfo factionManagerAllFactions;

		private bool mapRerollTriggered;
		private string originalWorldSeed;
		private GameInitData capturedInitData;
		private SerializedPawns cannedColonists;
		private string stockLoadingMessage;
		private int numAvailableLoadingMessages;
		private int originalMapResourceCount;

		private SettingHandle<bool> settingPaidRerolls; 
		private SettingHandle<bool> settingLoadingMessages; 
		private SettingHandle<int>  settingWidgetSize;
		private SettingHandle<bool> settingLogConsumedResources;

		private MapRerollController() {
			instance = this;
			guiWidget = new RerollGUIWidget();
		}

		public override void Initalize() {
			if (DefDatabase<MapGeneratorDef>.AllDefs.ToArray().Length > 1) {
				Logger.Warning("There is more than one MapGeneratorDef in the database. Cannot guarantee consistent behaviour.");
			}
			PrepareReflectionReferences();
			numAvailableLoadingMessages = CountAvailableLoadingMessages();
			PrepareSettingsHandles();
		}

		public override void MapComponentsInitializing() {
			capturedInitData = Current.Game.InitData;
			cannedColonists = new SerializedPawns(capturedInitData.startingPawns);
		}

		public override void MapLoaded() {
			if(!ModIsActive) return;
			if(capturedInitData == null) return; // map was loaded from save
			
			ResetScattererGensteps();

			originalMapResourceCount = GetAllResourcesOnMap().Count;
			if (!settingPaidRerolls) {
				ResourcePercentageRemaining = 100f;
			}
			if(mapRerollTriggered) {
				if (settingPaidRerolls) {
					// adjust map to current remaining resources and charge for the reroll
					ReduceMapResources(100 - (ResourcePercentageRemaining), 100); 
					SubtractResourcePercentage(SettingsDef.mapRerollCost);
				}
				Find.World.info.seedString = originalWorldSeed;
				KillIntroDialog();
				RestoreVanillaLoadingMessage();
				if(OnMapRerolled!=null) OnMapRerolled();
			} else {
				ResourcePercentageRemaining = 100f;
				originalWorldSeed = Find.World.info.seedString;
			}
			guiWidget.Initialize(mapRerollTriggered);
			mapRerollTriggered = false;
		}

		public override void OnGUI() {
			guiWidget.OnGUI();
		}

		public bool CanAffordOperation(MapRerollType type) {
			float cost = 0;
			switch (type) {
				case MapRerollType.Map: cost = SettingsDef.mapRerollCost; break;
				case MapRerollType.Geyser: cost = SettingsDef.geyserRerollCost; break;
			}
			return !settingPaidRerolls || ResourcePercentageRemaining >= cost;
		}

		public void RerollGeysers() {
			try {
				var geyserGen = TryGetGeyserGenstep();
				if (geyserGen != null) {
					TryGenerateGeysersWithNewLocations(geyserGen);
					if (settingPaidRerolls) SubtractResourcePercentage(SettingsDef.geyserRerollCost);
					if (OnGeysersRerolled != null) OnGeysersRerolled();
				} else {
					Logger.Error("Failed to find the Genstep for geysers. Check your map generator config.");
				}
			} catch (Exception e) {
				Logger.ReportException("RerollGeysers", e);
			}
		}

		public void RerollMap() {
			if (mapRerollTriggered) return;
			try {
				mapRerollTriggered = true;
				Find.Selector.SelectedObjects.Clear();

				var sameWorld = Current.Game.World;
				var sameScenario = Current.Game.Scenario;
				var sameStoryteller = Current.Game.storyteller;

				// relationships on off-map pawns need to be cleaned up to avoid dangling references
				foreach (var startingPawn in capturedInitData.startingPawns) {
					if(!startingPawn.Destroyed) startingPawn.Destroy();
				}

				// discard the old player faction so that scenarios can do their thing
				DiscardWorldSquareFactions(capturedInitData.startingCoords);

				Current.ProgramState = ProgramState.Entry;
				Current.Game = new Game();
				var newInitData = Current.Game.InitData = new GameInitData();
				Current.Game.Scenario = sameScenario;
				Find.Scenario.PreConfigure();
				newInitData.permadeath = capturedInitData.permadeath;
				newInitData.startingCoords = capturedInitData.startingCoords;
				newInitData.startingMonth = capturedInitData.startingMonth;
				newInitData.mapSize = capturedInitData.mapSize;

				Current.Game.World = sameWorld;
				Current.Game.storyteller = sameStoryteller;
				sameWorld.info.seedString = Rand.Int.ToString();

				Find.Scenario.PostWorldLoad();
				MapIniter_NewGame.PrepForMapGen();
				Find.Scenario.PreMapGenerate();

				// trash all newly generated pawns
				StartingPawnUtility.ClearAllStartingPawns();

				// restore our serialized starting colonists
				newInitData.startingPawns = cannedColonists.ToList();
				foreach (var startingPawn in newInitData.startingPawns) {
					startingPawn.SetFactionDirect(newInitData.playerFaction);
				}
				PrepareCarefullyCompat.UpdateCustomColonists(newInitData.startingPawns);

				// precaution against social tab errors, this shouldn't be necessary
				foreach (var relative in cannedColonists.GetOffMapRelatives()) {
					if (relative.Faction.RelationWith(newInitData.playerFaction, true) == null) {
						relative.Faction.TryMakeInitialRelationsWith(newInitData.playerFaction);
					}
				}

				SetCustomLoadingMessage();
				LongEventHandler.QueueLongEvent(() => { }, "Map", "GeneratingMap", true, null);
			} catch (Exception e) {
				Logger.ReportException("RerollMap", e);
			}
		}

		private void SetCustomLoadingMessage() {
			var customLoadingMessage = stockLoadingMessage = LoadingMessageKey.Translate();

			if (settingLoadingMessages && numAvailableLoadingMessages > 0) {
				var messageIndex = Rand.Range(0, numAvailableLoadingMessages - 1);
				var messageKey = CustomLoadingMessagePrefix + messageIndex;
				if (messageKey.CanTranslate()) {
					customLoadingMessage = messageKey.Translate();
				}
			}

			LanguageDatabase.activeLanguage.keyedReplacements[LoadingMessageKey] = customLoadingMessage;
		}

		private void RestoreVanillaLoadingMessage() {
			LanguageDatabase.activeLanguage.keyedReplacements[LoadingMessageKey] = stockLoadingMessage;
		}

		private void PrepareReflectionReferences() {
			thingPrivateStateField = typeof(Thing).GetField("thingStateInt", BindingFlags.Instance | BindingFlags.NonPublic);
			genstepScattererProtectedUsedSpots = typeof(GenStep_Scatterer).GetField("usedSpots", BindingFlags.Instance | BindingFlags.NonPublic);
			factionManagerAllFactions = typeof(FactionManager).GetField("allFactions", BindingFlags.Instance | BindingFlags.NonPublic);
			if (thingPrivateStateField == null || genstepScattererProtectedUsedSpots == null || factionManagerAllFactions == null) {
				Logger.Error("Failed to get named fields by reflection");
			}
		}

		private void DiscardWorldSquareFactions(IntVec2 square) {
			var factionList = (List<Faction>)factionManagerAllFactions.GetValue(Find.FactionManager);
			Faction faction;
			while ((faction = Find.FactionManager.FactionInWorldSquare(square)) != null) {
				faction.RemoveAllRelations();
				factionList.Remove(faction);	
			}
		}

		// Genstep_Scatterer instances build up internal state during generation
		// if not reset, after enough rerolls, the map generator will fail to find spots to place geysers, items, resources, etc.
		private void ResetScattererGensteps() {
			var mapGenDef = DefDatabase<MapGeneratorDef>.AllDefs.FirstOrDefault();
			if (mapGenDef == null) return;
			foreach (var genStepDef in mapGenDef.GenStepsInOrder) {
				var genstepScatterer = genStepDef.genStep as GenStep_Scatterer;
				if (genstepScatterer != null) {
					ResetScattererGenstepInternalState(genstepScatterer);		
				}
			}
		}

		private void ResetScattererGenstepInternalState(GenStep_Scatterer genstep) {
			// field is protected, use reflection
			var usedSpots = (HashSet<IntVec3>) genstepScattererProtectedUsedSpots.GetValue(genstep);
			if(usedSpots!=null) {
				usedSpots.Clear();
			}
		}

		// Genstep_ScatterThings is prone to generating things in the same spot on occasion.
		// If that happens we try to run it a few more times to try and get new positions.
		private void TryGenerateGeysersWithNewLocations(GenStep_ScatterThings geyserGen) {
			const int MaxGeyserGenerationAttempts = 5;
			var collisionsDetected = true;
			var attemptsRemaining = MaxGeyserGenerationAttempts;
			while (attemptsRemaining>0 && collisionsDetected) {
				var usedSpots = new HashSet<IntVec3>(GetAllGeyserPositionsOnMap());
				// destroy existing geysers
				Thing.allowDestroyNonDestroyable = true;
				Find.ListerThings.ThingsOfDef(ThingDefOf.SteamGeyser).ForEach(t => t.Destroy());
				Thing.allowDestroyNonDestroyable = false;
				// make new geysers
				geyserGen.Generate();
				// clean up internal state
				ResetScattererGenstepInternalState(geyserGen);
				// check if some geysers were generated in the same spots
				collisionsDetected = false;
				foreach (var geyserPos in GetAllGeyserPositionsOnMap()) {
					if(usedSpots.Contains(geyserPos)) {
						collisionsDetected = true;
					}
				}
				attemptsRemaining--;
			}
		}

		private IEnumerable<IntVec3> GetAllGeyserPositionsOnMap() {
			return Find.ListerThings.ThingsOfDef(ThingDefOf.SteamGeyser).Select(t => t.Position);
		}

		private void ReduceMapResources(float consumePercent, float currentResourcesAtPercent) {
			if (currentResourcesAtPercent == 0) return;
			var rockDef = Find.World.NaturalRockTypesIn(Find.Map.WorldCoords).FirstOrDefault();
			var mapResources = GetAllResourcesOnMap();
			
			var newResourceAmount = Mathf.Clamp(currentResourcesAtPercent - consumePercent, 0, 100);
			var originalResAmount = Mathf.Ceil(mapResources.Count / (currentResourcesAtPercent/100));
			var percentageChange = currentResourcesAtPercent - newResourceAmount;
			var resourceToll = (int)Mathf.Ceil(Mathf.Abs(originalResAmount * (percentageChange/100)));

			var toll = resourceToll;
			if (mapResources.Count > 0) {
				// eat random resources
				while (mapResources.Count > 0 && toll > 0) {
					var resIndex = Random.Range(0, mapResources.Count);
					var resThing = mapResources[resIndex];

					SneakilyDestroyResource(resThing);
					mapResources.RemoveAt(resIndex);
					if (rockDef != null && !RollForPirateStash(resThing.Position)) {
						// put some rock in their place
						var rock = ThingMaker.MakeThing(rockDef);
						GenPlace.TryPlaceThing(rock, resThing.Position, ThingPlaceMode.Direct);
					}
					toll--;
				}
			}
			if (!Prefs.DevMode || !settingLogConsumedResources) return;
			Logger.Message("Ordered to consume " + consumePercent + "%, with current resources at " + currentResourcesAtPercent + "%. Consuming " + resourceToll + " resource spots, " + mapResources.Count + " left");
			if (toll > 0) Logger.Message("Failed to consume " + toll + " resource spots.");
		}

		private void SubtractResourcePercentage(float percent) {
			ReduceMapResources(percent, ResourcePercentageRemaining);
			ResourcePercentageRemaining = Mathf.Clamp(ResourcePercentageRemaining - percent, 0, 100);
		}

		private List<Thing> GetAllResourcesOnMap() {
			return Find.ListerThings.AllThings.Where(t => t.def != null && t.def.building != null && t.def.building.mineableScatterCommonality > 0).ToList();
		}

		private bool RollForPirateStash(IntVec3 position) {
			var stashDef = MapRerollDefOf.PirateStash.building as BuildingProperties_PirateStash;
			if (stashDef == null || originalMapResourceCount == 0 || stashDef.commonality == 0 || !LocationIsSuitableForStash(position)) return false;
			var mapSize = Find.Map.Size;
			// bias 0 for default map size, bias 1 for max map size
			var mapSizeBias = ((((mapSize.x*mapSize.z)/DefaultMapSize) - 1)/((LargestMapSize/DefaultMapSize) - 1)) * stashDef.mapSizeCommonalityBias;
			var rollSuccess = Rand.Range(0f, 1f) < 1/(originalMapResourceCount/(stashDef.commonality + mapSizeBias));
			if (!rollSuccess) return false;
			var stash = ThingMaker.MakeThing(MapRerollDefOf.PirateStash);
			GenSpawn.Spawn(stash, position);
			return true;
		}

		// check for double-thick walls to prevent players peeking through fog
		private bool LocationIsSuitableForStash(IntVec3 position) {
			var grid = Find.EdificeGrid;
			if (!position.InBounds() || grid[CellIndices.CellToIndex(position)] != null) return false;
			for (int i = 0; i < 4; i++) {
				if (grid[CellIndices.CellToIndex(position + GenAdj.CardinalDirections[i])] == null) return false;
				if (grid[CellIndices.CellToIndex(position + GenAdj.CardinalDirections[i] * 2)] == null) return false;
			}
			return true;
		}

		// destroying a resource outright causes too much overhead: fog, area reveal, pathing, roof updates, etc
		// we just want to replace it. So, we just despawn it and do some cleanup.
		// As of A13 despawning triggers all of the above. So, we do all the cleanup manually.
		// The following is Thing.Despawn code with the unnecessary parts stripped out, plus key parts from Building.Despawn
		// TODO: This approach may break with future releases (if thing despawning changes), so it's worth checking over.
		private void SneakilyDestroyResource(Thing res) {
			Find.Map.listerThings.Remove(res);
			Find.ThingGrid.Deregister(res);
			Find.CoverGrid.DeRegister(res);
			if (res.def.hasTooltip) {
				Find.TooltipGiverList.DeregisterTooltipGiver(res);
			}
			if (res.def.graphicData != null && res.def.graphicData.Linked) {
				LinkGrid.Notify_LinkerCreatedOrDestroyed(res);
				Find.MapDrawer.MapMeshDirty(res.Position, MapMeshFlag.Things, true, false);
			}
			Find.Selector.Deselect(res);
			if (res.def.drawerType != DrawerType.RealtimeOnly) {
				var cellRect = res.OccupiedRect();
				for (var i = cellRect.minZ; i <= cellRect.maxZ; i++) {
					for (var j = cellRect.minX; j <= cellRect.maxX; j++) {
						Find.Map.mapDrawer.MapMeshDirty(new IntVec3(j, 0, i), MapMeshFlag.Things);
					}
				}
			}
			if (res.def.drawerType != DrawerType.MapMeshOnly) {
				Find.DynamicDrawManager.DeRegisterDrawable(res);
			}
			thingPrivateStateField.SetValue(res, res.def.DiscardOnDestroyed ? ThingState.Discarded : ThingState.Memory);
			Find.TickManager.DeRegisterAllTickabilityFor(res);
			Find.AttackTargetsCache.Notify_ThingDespawned(res);
			// building-specific cleanup
			if(res.def.IsEdifice()) Find.EdificeGrid.DeRegister((Building)res);
			Find.ListerBuildings.Remove((Building) res);
			Find.DesignationManager.RemoveAllDesignationsOn(res);
		}

		private GenStep_ScatterThings TryGetGeyserGenstep() {
			var mapGenDef = DefDatabase<MapGeneratorDef>.AllDefs.FirstOrDefault();
			if (mapGenDef == null) return null;
			return (GenStep_ScatterThings)mapGenDef.GenStepsInOrder.Find(g => {
				var gen = g.genStep as GenStep_ScatterThings;
				return gen != null && gen.thingDef == ThingDefOf.SteamGeyser;
			}).genStep;
		}

		private void KillIntroDialog(){
			Find.WindowStack.TryRemove(typeof(Dialog_NodeTree), false);
		}

		private int CountAvailableLoadingMessages() {
			for (int i = 0; i < 1000; i++) {
				if((CustomLoadingMessagePrefix + i).CanTranslate()) continue;
				return i;
			}
			return 0;
		}

		private void PrepareSettingsHandles() {
			settingPaidRerolls = Settings.GetHandle("paidRerolls", "setting_paidRerolls_label".Translate(), "setting_paidRerolls_desc".Translate(), true);
			settingLoadingMessages = Settings.GetHandle("loadingMessages", "setting_loadingMessages_label".Translate(), "setting_loadingMessages_desc".Translate(), true);
			settingWidgetSize = Settings.GetHandle("widgetSize", "setting_widgetSize_label".Translate(), "setting_widgetSize_desc".Translate(), DefaultWidgetSize, Validators.IntRangeValidator(MinWidgetSize, MaxWidgetSize));
			settingLogConsumedResources = Settings.GetHandle("logConsumption", "setting_logConsumption_label".Translate(), "setting_logConsumption_desc".Translate(), false);
			settingLogConsumedResources.VisibilityPredicate = () => Prefs.DevMode;
		}
	}
}