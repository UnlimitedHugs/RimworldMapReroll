using System;
using System.Collections.Generic;
using System.Linq;
using HugsLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MapReroll {
	public enum PaidOperationType {
		GeneratePreviews, RerollGeysers
	}

	public static class RerollToolbox {
		private const sbyte ThingMemoryState = -2;
		private const sbyte ThingDiscardedState = -3;

		public static void DoMapReroll(string seed = null) {
			var oldMap = Find.CurrentMap;
			if (oldMap == null) {
				MapRerollController.Instance.Logger.Error("No visible map- cannot reroll");
				return;
			}
			LoadingMessages.SetCustomLoadingMessage(MapRerollController.Instance.LoadingMessagesSetting);
			var oldParent = (MapParent)oldMap.ParentHolder;
			var isOnStartingTile = MapIsOnStartingTile(oldMap, MapRerollController.Instance.WorldState);
			var originalTile = MoveMapParentSomewhereElse(oldParent);

			if (isOnStartingTile) Current.Game.InitData = MakeInitData(MapRerollController.Instance.WorldState, oldMap);

			var oldMapState = GetStateForMap(oldMap);
			var playerPawns = GetAllPlayerPawnsOnMap(oldMap); // includes animals
			var colonists = GetAllPlayerPawnsOnMap(oldMap).Where(p => p.IsColonist).ToList();
			IEnumerable<Thing> nonGeneratedThings = ResolveThingsFromIds(oldMap, oldMapState.PlayerAddedThingIds).ToList();

			// discard the old map in main thread
			LongEventHandler.QueueLongEvent(() => {
				// wipe world things generated with map to prevent player from sneaking out items, then rerolling
				if (oldMapState.ScenarioGeneratedThingIds.Count > 0 && MapRerollController.Instance.AntiCheeseSetting) {
					try {
						ClearRelationsWithPawns(colonists, oldMapState.ScenarioGeneratedThingIds);
						DestroyThingsInWorldById(oldMapState.ScenarioGeneratedThingIds);
					} catch (Exception e) {
						// unknown mod conflict is afoot: https://gist.github.com/HugsLibRecordKeeper/f9278475f338b5d564bf7b9f7f6642d7
						// since this is a non-critical section we can just suppress the error for now
						MapRerollController.Instance.Logger.Warning("Caught exception while trying to destroy world things from discarded map: " + e);
					}
				}
				DespawnThings(playerPawns.OfType<Thing>(), oldMap);
				DespawnThings(nonGeneratedThings, oldMap);
				DiscardFactionBase(oldParent);
				StripMap(oldMap);
			}, "GeneratingMap", false, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);

			// generate new map in work thread
			LongEventHandler.QueueLongEvent(() => {
				ResetIncidentScenarioParts(Find.Scenario);

				var newParent = PlaceNewMapParent(originalTile);

				if (Find.TickManager.CurTimeSpeed == TimeSpeed.Paused) {
					MapRerollController.Instance.PauseOnNextLoad();
				}

				var mapSeed = seed ?? GetNextRerollSeed(CurrentMapSeed(oldMapState));

				MapRerollController.HasCavesOverride.OverrideEnabled = true;
				MapRerollController.HasCavesOverride.HasCaves = Find.World.HasCaves(originalTile);
				var newMap = GenerateNewMapWithSeed(newParent, Find.World.info.initialMapSize, mapSeed);
				
				var newMapState = GetStateForMap(newMap);
				newMapState.RerollGenerated = true;
				newMapState.PlayerAddedThingIds = oldMapState.PlayerAddedThingIds;
				newMapState.ResourceBalance = oldMapState.ResourceBalance;
				newMapState.RerollSeed = mapSeed;
				newMapState.NumPreviewPagesPurchased = 0;

				SwitchToMap(newMap);
				if (isOnStartingTile) {
					Find.Scenario.PostGameStart();
					Current.Game.InitData = null;
				}

				// spawn things in main thread to ensure all map sections have finished generating
				HugsLibController.Instance.DoLater.DoNextUpdate(() => {
					try {
						var viableCenter = FindViableDropSpotCenter(newMap);
						if (!isOnStartingTile) {
							SpawnPawnsOnMap(playerPawns, newMap, viableCenter);
						}
						SpawnThingsOnMap(nonGeneratedThings, newMap, viableCenter);
					} catch (Exception e) {
						GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap(e);
					}
				});

				MapRerollController.HasCavesOverride.OverrideEnabled = false;
				LoadingMessages.RestoreVanillaLoadingMessage();
			}, "GeneratingMap", true, e => {
				MapRerollController.HasCavesOverride.OverrideEnabled = false;
				GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap(e);
			});
		}

		public static string CurrentMapSeed(RerollMapState mapState) {
			return mapState.RerollSeed ?? Find.World.info.seedString;
		}

		public static string GetNextRerollSeed(string currentSeed) {
			return MapRerollController.Instance.DeterministicRerollsSetting ? GenerateNewRerollSeed(currentSeed) : Rand.Int.ToString();
		}

		public static RerollMapState GetStateForMap(Map map = null) {
			if (map == null) map = Find.CurrentMap;
			if (map == null) {
				MapRerollController.Instance.Logger.Error("Cannot get state from null map. VisibleMap was null, as well: " + Environment.StackTrace);
				return null;
			}
			var comp = map.GetComponent<MapComponent_MapRerollState>();
			if (comp == null) {
				MapRerollController.Instance.Logger.Error($"Could not get MapComponent_MapRerollState from map {map}: {Environment.StackTrace}");
				return null;
			}
			return comp.State ?? (comp.State = new RerollMapState());
		}

		public static void KillMapIntroDialog() {
			Find.WindowStack.TryRemove(typeof(Dialog_NodeTree), false);
		}

		public static void RecordPlayerAddedMapThings(IThingHolder owner, Map onMap) {
			var state = GetStateForMap(onMap);
			var knownOrInvalidThingIds = new HashSet<int>(state.PlayerAddedThingIds.Union(state.ScenarioGeneratedThingIds));
			var nonColonistThings = ThingOwnerUtility.GetAllThingsRecursively(owner, false)
				.Where(t => !(t is Pawn) && !(t is Building) && !knownOrInvalidThingIds.Contains(t.thingIDNumber));
			//Logger.Message("Player added things to map: " + nonColonistThings.ListElements());
			state.PlayerAddedThingIds.AddRange(nonColonistThings.Select(t => t.thingIDNumber));
		}

		public static void StoreGeneratedThingIdsInMapState(Map map) {
			var state = GetStateForMap(map);
			var generatedThingIds = GetMapThingsAndPawnsExceptColonists(map).Select(t => t.thingIDNumber);
			state.ScenarioGeneratedThingIds = generatedThingIds.ToList();
		}

		public static void ReduceMapResources(Map map, float consumePercent, float resourcesPercentBalance) {
			if (resourcesPercentBalance == 0) return;
			var rockTypes = Find.World.NaturalRockTypesIn(map.Tile).ToList();
			var mapResources = GetAllResourcesOnMap(map);

			var newResourceAmount = Mathf.Clamp(resourcesPercentBalance - consumePercent, 0, 100);
			var originalResAmount = Mathf.CeilToInt(mapResources.Count / (resourcesPercentBalance / 100));
			var percentageChange = resourcesPercentBalance - newResourceAmount;
			var resourceToll = Mathf.CeilToInt(Mathf.Abs(originalResAmount * (percentageChange / 100)));

			var toll = resourceToll;
			if (mapResources.Count > 0) {
				// eat random resources
				for (int i = 0; i < mapResources.Count && toll > 0; i++) {
					var resThing = mapResources[i];

					SneakilyDestroyResource(resThing);

					// put some rock in their place
					var rockDef = FindAdjacentRockDef(map, resThing.Position, rockTypes);
					var rock = ThingMaker.MakeThing(rockDef);
					GenPlace.TryPlaceThing(rock, resThing.Position, map, ThingPlaceMode.Direct);
					toll--;
				}
			}
			if (MapRerollController.Instance.LogConsumedResourcesSetting && Prefs.DevMode) {
				MapRerollController.Instance.Logger.Message("Ordered to consume " + consumePercent + "%, with current resources at " + resourcesPercentBalance + "%. Consuming " +
															resourceToll + " resource spots, " + mapResources.Count + " left");
				if (toll > 0) MapRerollController.Instance.Logger.Message("Failed to consume " + toll + " resource spots.");
			}

		}

		public static void SubtractResourcePercentage(Map map, float percent) {
			var rerollState = GetStateForMap(map);
			ReduceMapResources(map, percent, rerollState.ResourceBalance);
			rerollState.ResourceBalance = Mathf.Clamp(rerollState.ResourceBalance - percent, 0f, 100f);
		}

		public static void ResetIncidentScenarioParts(Scenario scenario) {
			foreach (var part in scenario.AllParts) {
				if (part != null && part.GetType() == ReflectionCache.ScenPartCreateIncidentType) {
					ReflectionCache.CreateIncident_IsFinished.SetValue(part, false);
				}
			}
		}

		public static void ChargeForOperation(PaidOperationType type, int desiredPreviewsPage = 0) {
			var map = Find.CurrentMap;
			var state = GetStateForMap(map);
			var cost = GetOperationCost(type, desiredPreviewsPage);
			if (cost > 0) {
				SubtractResourcePercentage(map, cost);
				if (type == PaidOperationType.GeneratePreviews) {
					state.NumPreviewPagesPurchased = desiredPreviewsPage+1;
				}
			}
		}

		public static float GetOperationCost(PaidOperationType type, int desiredPreviewsPage = 0) {
			if (MapRerollController.Instance.PaidRerollsSetting) {
				switch (type) {
					case PaidOperationType.RerollGeysers: return Resources.Settings.MapRerollSettings.geyserRerollCost;
					case PaidOperationType.GeneratePreviews:
						var mapState = GetStateForMap();
						var numOutstandingPages = Mathf.Max(0, desiredPreviewsPage - (mapState.NumPreviewPagesPurchased - 1));
						return numOutstandingPages > 0 ? Resources.Settings.MapRerollSettings.previewPageCost * numOutstandingPages : 0;
				}
			}
			return 0;
		}

		private static List<Thing> GetAllResourcesOnMap(Map map) {
			return map.listerThings.AllThings.Where(t => t.def?.building != null && t.def.building.mineableScatterCommonality > 0)
				.OrderBy(HasAdjacentNaturalRockComparator).ToList();
		}

		private static int HasAdjacentNaturalRockComparator(Thing t) {
			// randomize element order as we check for adjacent rocks
			for (int i = 0; i < GenAdj.CardinalDirectionsAround.Length; i++) {
				var adjacentPos = t.Position + GenAdj.CardinalDirectionsAround[i];
				var adjacentThing = t.Map.edificeGrid[adjacentPos];
				if (adjacentThing?.def?.building != null && adjacentThing.def.building.isNaturalRock && !adjacentThing.def.building.isResourceRock) {
					return Rand.Range(0, int.MaxValue / 2);
				}
			}
			return Rand.Range(int.MaxValue / 2, int.MaxValue);
		}

		private static ThingDef FindAdjacentRockDef(Map map, IntVec3 pos, List<ThingDef> viableRockTypes) {
			for (int i = 0; i < GenAdj.CardinalDirectionsAround.Length; i++) {
				var adjacent = pos + GenAdj.CardinalDirectionsAround[i];
				var adjacentThing = map.edificeGrid[adjacent];
				if (adjacentThing?.def?.building != null && adjacentThing.def.building.isNaturalRock && viableRockTypes.Contains(adjacentThing.def)) {
					return adjacentThing.def;
				}
			}
			return viableRockTypes[0];
		}

		public static void TryStopPawnVomiting(Map map) {
			if (!MapRerollController.Instance.NoVomitingSetting) return;
			foreach (var pawn in GetAllPlayerPawnsOnMap(map)) {
				foreach (var hediff in pawn.health.hediffSet.hediffs) {
					if (hediff.def != HediffDefOf.CryptosleepSickness) continue;
					pawn.health.RemoveHediff(hediff);
					break;
				}
			}
		}

		public static List<KeyValuePair<int, string>> GetAvailableMapSizes() {
			return new List<KeyValuePair<int, string>> {
				new KeyValuePair<int, string>(200, "MapSizeSmall".Translate()),
				new KeyValuePair<int, string>(225, null),
				new KeyValuePair<int, string>(250, "MapSizeMedium".Translate()),
				new KeyValuePair<int, string>(275, null),
				new KeyValuePair<int, string>(300, "MapSizeLarge".Translate()),
				new KeyValuePair<int, string>(325, null),
				new KeyValuePair<int, string>(350, "MapSizeExtreme".Translate()),
				new KeyValuePair<int, string>(400, null),
			};
		}

		/// <summary>
		/// destroying a resource outright causes too much overhead: fog, area reveal, pathing, roof updates, etc
		///	we just want to replace it. So, we manually strip it out of the map and do some cleanup.
		/// The following is Thing.Despawn code with the unnecessary (for buildings, ar least) parts stripped out, plus key parts from Building.Despawn 
		/// TODO: This approach may break with future releases (if thing despawning changes), so it's worth checking over.
		/// </summary>
		private static void SneakilyDestroyResource(Thing res) {
			var map = res.Map;
			RegionListersUpdater.DeregisterInRegions(res, map);
			map.spawnedThings.Remove(res);
			map.listerThings.Remove(res);
			map.thingGrid.Deregister(res);
			map.coverGrid.DeRegister(res);
			map.tooltipGiverList.Notify_ThingDespawned(res);
			if (res.def.graphicData != null && res.def.graphicData.Linked) {
				map.linkGrid.Notify_LinkerCreatedOrDestroyed(res);
				map.mapDrawer.MapMeshDirty(res.Position, MapMeshFlag.Things, true, false);
			}
			Find.Selector.Deselect(res);
			res.DirtyMapMesh(map);
			if (res.def.drawerType != DrawerType.MapMeshOnly) {
				map.dynamicDrawManager.DeRegisterDrawable(res);
			}
			ReflectionCache.Thing_State.SetValue(res, res.def.DiscardOnDestroyed ? ThingDiscardedState : ThingMemoryState);
			Find.TickManager.DeRegisterAllTickabilityFor(res);
			map.attackTargetsCache.Notify_ThingDespawned(res);
			StealAIDebugDrawer.Notify_ThingChanged(res);
			// building-specific cleanup
			var b = (Building)res;
			if (res.def.IsEdifice()) map.edificeGrid.DeRegister(b);
			var sustainer = (Sustainer)ReflectionCache.Building_SustainerAmbient.GetValue(res);
			if (sustainer != null) sustainer.End();
			map.mapDrawer.MapMeshDirty(b.Position, MapMeshFlag.Buildings);
			map.glowGrid.MarkGlowGridDirty(b.Position);
			map.listerBuildings.Remove((Building)res);
			map.listerBuildingsRepairable.Notify_BuildingDeSpawned(b);
			map.designationManager.Notify_BuildingDespawned(b);
		}

		private static void DestroyThingsInWorldById(IEnumerable<int> idsToDestroy) {
			var idSet = new HashSet<int>(idsToDestroy);
			var things = new List<Thing>();
			ThingOwnerUtility.GetAllThingsRecursively(Find.World, things, false);
			for (int i = 0; i < things.Count; i++) {
				var t = things[i];
				if (idSet.Contains(t.thingIDNumber) && !t.Destroyed) {
					t.Destroy();
				}
			}
		}

		private static void SpawnPawnsOnMap(IEnumerable<Pawn> pawns, Map map, IntVec3 dropCenter) {
			foreach (var pawn in pawns) {
				if (pawn.Destroyed) continue;
				if (!DropCellFinder.TryFindDropSpotNear(dropCenter, map, out IntVec3 pos, false, false)) {
					pos = dropCenter;
					MapRerollController.Instance.Logger.Warning("Could not find drop spot for pawn {0} on map {1}", pawn, map);
				}
				GenSpawn.Spawn(pawn, pos, map);
			}
		}

		private static IEnumerable<Thing> ResolveThingsFromIds(Map map, IEnumerable<int> thingIds) {
			var idSet = new HashSet<int>(thingIds);
			return map.listerThings.AllThings.Where(t => idSet.Contains(t.thingIDNumber));
		}

		private static void SpawnThingsOnMap(IEnumerable<Thing> things, Map map, IntVec3 dropCenter) {
			foreach (var thing in things) {
				if (thing.Destroyed || thing.Spawned) continue;
				if (!DropCellFinder.TryFindDropSpotNear(dropCenter, map, out IntVec3 pos, false, false)) {
					pos = dropCenter;
				}
				if (!GenPlace.TryPlaceThing(thing, pos, map, ThingPlaceMode.Near)) {
					GenSpawn.Spawn(thing, pos, map);
					MapRerollController.Instance.Logger.Warning("Could not find drop spot for thing {0} on map {1}", thing, map);
				}
			}
		}

		private static IntVec3 FindViableDropSpotCenter(Map map) {
			const int viableCenterSearchRadius = 50;
			var center = map.Center;
			foreach (var c in GenRadial.RadialCellsAround(center, viableCenterSearchRadius, true)) {
				if (DropCellFinder.IsGoodDropSpot(c, map, false, false)) {
					return c;
				}
			}
			return center;
		}

		private static GameInitData MakeInitData(RerollWorldState state, Map sourceMap) {
			var colonists = GetAllPlayerPawnsOnMap(sourceMap).Where(p => p.IsColonist).ToList();
			return new GameInitData {
				permadeath = Find.GameInfo.permadeathMode,
				mapSize = sourceMap.Size.x,
				playerFaction = Faction.OfPlayer,
				startingSeason = Season.Undefined,
				startedFromEntry = true,
				startingTile = state.StartingTile,
				startingAndOptionalPawns = colonists,
				startingPawnCount = colonists.Count
			};
		}

		private static bool MapIsOnStartingTile(Map map, RerollWorldState state) {
			var mapParent = (MapParent)map.ParentHolder;
			if (mapParent == null) return false;
			return mapParent.Tile == state.StartingTile;
		}

		// clears references to map components so that they may be garbage-collected even if the map itself isn't
		private static void StripMap(Map map) {
			map.spawnedThings = null;
			map.cellIndices = null;
			map.listerThings = null;
			map.listerBuildings = null;
			map.mapPawns = null;
			map.dynamicDrawManager = null;
			map.mapDrawer = null;
			map.tooltipGiverList = null;
			map.pawnDestinationReservationManager = null;
			map.reservationManager = null;
			map.physicalInteractionReservationManager = null;
			map.designationManager = null;
			map.lordManager = null;
			map.debugDrawer = null;
			map.passingShipManager = null;
			map.haulDestinationManager = null;
			map.gameConditionManager = null;
			map.weatherManager = null;
			map.zoneManager = null;
			map.resourceCounter = null;
			map.mapTemperature = null;
			map.temperatureCache = null;
			map.areaManager = null;
			map.attackTargetsCache = null;
			map.attackTargetReservationManager = null;
			map.lordsStarter = null;
			map.thingGrid = null;
			map.coverGrid = null;
			map.edificeGrid = null;
			map.blueprintGrid = null;
			map.fogGrid = null;
			map.glowGrid = null;
			map.regionGrid = null;
			map.terrainGrid = null;
			map.pathGrid = null;
			map.roofGrid = null;
			map.fertilityGrid = null;
			map.snowGrid = null;
			map.deepResourceGrid = null;
			map.exitMapGrid = null;
			map.linkGrid = null;
			map.glowFlooder = null;
			map.powerNetManager = null;
			map.powerNetGrid = null;
			map.regionMaker = null;
			map.pathFinder = null;
			map.pawnPathPool = null;
			map.regionAndRoomUpdater = null;
			map.regionLinkDatabase = null;
			map.moteCounter = null;
			map.gatherSpotLister = null;
			map.windManager = null;
			map.listerBuildingsRepairable = null;
			map.listerHaulables = null;
			map.listerMergeables = null;
			map.listerFilthInHomeArea = null;
			map.reachability = null;
			map.itemAvailability = null;
			map.autoBuildRoofAreaSetter = null;
			map.roofCollapseBufferResolver = null;
			map.roofCollapseBuffer = null;
			map.wildAnimalSpawner = null;
			map.wildPlantSpawner = null;
			map.steadyEnvironmentEffects = null;
			map.skyManager = null;
			map.overlayDrawer = null;
			map.floodFiller = null;
			map.weatherDecider = null;
			map.fireWatcher = null;
			map.dangerWatcher = null;
			map.damageWatcher = null;
			map.strengthWatcher = null;
			map.wealthWatcher = null;
			map.regionDirtyer = null;
			map.cellsInRandomOrder = null;
			map.rememberedCameraPos = null;
			map.mineStrikeManager = null;
			map.storyState = null;
			map.retainedCaravanData = null;
			map.components.Clear();
		}

		private static void DiscardFactionBase(MapParent mapParent) {
			Current.Game.DeinitAndRemoveMap(mapParent.Map);
			Find.WorldObjects.Remove(mapParent);
		}
		
		private static void SwitchToMap(Map newMap) {
			Current.Game.CurrentMap = newMap;
			Find.World.renderer.wantedMode = WorldRenderMode.None;
		}

		private static Map GenerateNewMapWithSeed(MapParent mapParent, IntVec3 size, string seed) {
			var prevSeed = Find.World.info.seedString;
			Find.World.info.seedString = seed;
			var newMap = GetOrGenerateMapUtility.GetOrGenerateMap(mapParent.Tile, size, null);
			Find.World.info.seedString = prevSeed;
			return newMap;
		}

		private static void ClearRelationsWithPawns(IEnumerable<Pawn> colonists, IEnumerable<int> thingIds) {
			var pawnIdsToForget = new HashSet<int>(thingIds);
			foreach (var pawn in colonists) {
				foreach (var relation in pawn.relations.DirectRelations.ToArray()) {
					if (relation.otherPawn != null && pawnIdsToForget.Contains(relation.otherPawn.thingIDNumber)) {
						pawn.relations.RemoveDirectRelation(relation);
					}
				}
			}
		}

		private static List<Pawn> GetAllPlayerPawnsOnMap(Map map) {
			return map.mapPawns.PawnsInFaction(Faction.OfPlayer).ToList();
		}

		private static void DespawnThings(IEnumerable<Thing> things, Map referenceMap) {
			foreach (var thing in things) {
				EjectThingFromContainer(thing, referenceMap);
				var pawn = thing as Pawn;
				if (pawn?.carryTracker?.CarriedThing != null) {
					Thing dropped;
					pawn.carryTracker.TryDropCarriedThing(thing.Position, ThingPlaceMode.Near, out dropped);
				}
				if (thing.Spawned) thing.DeSpawn();
			}
		}

		private static MapParent PlaceNewMapParent(int worldTile) {
			var newParent = (MapParent)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
			newParent.Tile = worldTile;
			newParent.SetFaction(Faction.OfPlayer);
			Find.WorldObjects.Add(newParent);
			return newParent;
		}

		private static int MoveMapParentSomewhereElse(MapParent oldParent) {
			var originalTile = oldParent.Tile;
			oldParent.Tile = TileFinder.RandomStartingTile();
			return originalTile;
		}

		private static IEnumerable<Thing> GetMapThingsAndPawnsExceptColonists(Map map) {
			var colonists = GetAllPlayerPawnsOnMap(map).Where(p => p.IsColonist).ToArray();
			return FilterOutWornApparel(GetAllHaulableThingsOnMap(map), colonists).Union(map.mapPawns.AllPawns.Except(colonists).OfType<Thing>());
		}

		private static List<Thing> GetAllHaulableThingsOnMap(Map map) {
			var things = new List<Thing>();
			var matchingThings = new List<Thing>();
			ThingOwnerUtility.GetAllThingsRecursively(map, things, false);
			for (int i = 0; i < things.Count; i++) {
				var thing = things[i];
				if (thing?.def != null && thing.def.EverHaulable) {
					matchingThings.Add(thing);
				}
			}
			return matchingThings;
		}

		private static IEnumerable<Thing> FilterOutWornApparel(IEnumerable<Thing> things, IEnumerable<Pawn> wornByPawns) {
			var apparel = wornByPawns.SelectMany(c => c.apparel.WornApparel).OfType<Thing>();
			return things.Except(apparel);
		}

		private static void EjectThingFromContainer(Thing thing, Map toMap) {
			var holdingMap = thing.Map;
			if (holdingMap == null && thing.holdingOwner != null) {
				thing.holdingOwner.Remove(thing);
				GenSpawn.Spawn(thing, toMap.Center, toMap);
			}
		}

		private static IEnumerable<Thing> FilterOutThingsWithIds(IEnumerable<Thing> things, IEnumerable<int> idsToRemove) {
			var idSet = new HashSet<int>(idsToRemove);
			return things.Where(t => !idSet.Contains(t.thingIDNumber));
		}

		private static string GenerateNewRerollSeed(string previousSeed) {
			const int magicNumber = 3;
			unchecked {
				return ((previousSeed.GetHashCode() << 1) * magicNumber).ToString();
			}
		}

		public static class LoadingMessages {
			private const string StockLoadingMessageKey = "GeneratingMap";
			private const string CustomLoadingMessagePrefix = "MapReroll_loading";

			private static string stockLoadingMessage;
			private static int numAvailableLoadingMessages;

			public static void UpdateAvailableLoadingMessageCount() {
				numAvailableLoadingMessages = CountAvailableLoadingMessages();
			}

			public static void SetCustomLoadingMessage(bool customMessagesEnabled) {
				stockLoadingMessage = StockLoadingMessageKey.Translate();
				var loadingMessage = stockLoadingMessage;
				if (customMessagesEnabled) {
					var msg = TryPickRandomCustomLoadingMessage();
					if (msg != null) loadingMessage = msg;
				}
				SetLoadingScreenMessage(loadingMessage);
			}

			public static void RestoreVanillaLoadingMessage() {
				SetLoadingScreenMessage(stockLoadingMessage);
			}

			private static string TryPickRandomCustomLoadingMessage() {
				if (numAvailableLoadingMessages > 0) {
					var messageIndex = Rand.Range(0, numAvailableLoadingMessages - 1);
					var messageKey = CustomLoadingMessagePrefix + messageIndex;
					if (messageKey.CanTranslate()) {
						return messageKey.Translate();
					}
				}
				return null;
			}

			private static void SetLoadingScreenMessage(string message) {
				var original = LanguageDatabase.activeLanguage.keyedReplacements[StockLoadingMessageKey];
				LanguageDatabase.activeLanguage.keyedReplacements[StockLoadingMessageKey] = new LoadedLanguage.KeyedReplacement {
					fileSource = original.fileSource,
					fileSourceFullPath = original.fileSourceFullPath,
					fileSourceLine = original.fileSourceLine,
					key = original.key,
					value = message
				};
			}

			private static int CountAvailableLoadingMessages() {
				for (int i = 0; i < 1000; i++) {
					if ((CustomLoadingMessagePrefix + i).CanTranslate()) continue;
					return i;
				}
				return 0;
			}
		}
	}
}