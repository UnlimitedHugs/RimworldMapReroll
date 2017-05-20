using System.Collections.Generic;
using System.Linq;
using HugsLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MapReroll {
	public static class MapRerollToolbox {
		private const sbyte ThingMemoryState = -2;
		private const sbyte ThingDiscardedState = -3;

		// create a deterministic but sufficiently random new seed
		public static string GenerateNewRerollSeed(string previousSeed) {
			const int magicNumber = 3;
			unchecked {
				return ((previousSeed.GetHashCode() << 1) * magicNumber).ToString();
			}
		}

		public static void PrepareColonistsForReroll(IEnumerable<Pawn> colonists) {
			var pawns = colonists.ToList();
			foreach (var pawn in pawns) {
				pawn.ClearMind();
				pawn.ClearReservations();
				pawn.needs.SetInitialLevels();
				pawn.equipment.DestroyAllEquipment();
				pawn.Drawer.renderer.graphics.flasher = new DamageFlasher(pawn); // fixes damaged pawns being stuck with red tint after reroll
			}
			DespawnPawns(pawns);
			RemoveAllBondedPetRelations(pawns);
		}

		public static List<Pawn> GetAllColonistsOnMap(Map map) {
			return map.mapPawns.PawnsInFaction(Faction.OfPlayer).Where(p => p.IsColonist).ToList();
		}

		public static void ClearShrineCasketOwnersFromWorld(Map map, World world) {
			// clear all shrine casket corpse owners from world. Since the map is discarded, those guys should not clog up the save file
			foreach (var thing in map.listerThings.ThingsOfDef(ThingDefOf.AncientCryptosleepCasket)) {
				var casket = thing as Building_AncientCryptosleepCasket;
				if(casket == null) continue;
				var corpse = casket.ContainedThing as Corpse;
				if (corpse == null || !world.worldPawns.Contains(corpse.InnerPawn)) continue;
				world.worldPawns.RemovePawn(corpse.InnerPawn);
			}
		}

		// discard the old player faction so that scenarios can do their thing
		public static void DiscardWorldTileFaction(int tile, World world) {
			var worldObjects = world.worldObjects;
			var faction = worldObjects.FactionBaseAt(tile).Faction;
			if (faction == null) return;
			// remove base
			var factionBases = worldObjects.FactionBases;
			for (int i = 0; i < factionBases.Count; i++) {
				if (factionBases[i].Tile != tile) continue;
				worldObjects.Remove(factionBases[i]);
				break;
			}
			// discard faction
			var factionList = Find.World.factionManager.AllFactionsListForReading;
			faction.RemoveAllRelations();
			factionList.Remove(faction);
		}

		// Reset ScenPart_CreateIncident to repeat their incident on the new map
		public static void ResetIncidentScenarioParts(Scenario scenario) {
			foreach (var part in scenario.AllParts) {
				if (part != null && part.GetType() == ReflectionCache.ScenPartCreateIncidentType) {
					ReflectionCache.CreateIncident_IsFinished.SetValue(part, false);
				}
			}
		}

		// pawns already start on the ground on rerolled maps, so make sure the tale of their initial landing is preserved
		public static void RecordPodLandingTaleForColonists(IEnumerable<Pawn> colonists, Scenario scenario) {
			var arrivalScenPart = (ScenPart_PlayerPawnsArriveMethod)scenario.AllParts.FirstOrDefault(s => s is ScenPart_PlayerPawnsArriveMethod);
			if (arrivalScenPart == null) return;
			var arrivalMethod = (PlayerPawnsArriveMethod)ReflectionCache.PawnArriveMethod_Method.GetValue(arrivalScenPart);
			if (arrivalMethod == PlayerPawnsArriveMethod.DropPods) {
				foreach (var pawn in colonists) {
					if (pawn.RaceProps.Humanlike) {
						TaleRecorder.RecordTale(TaleDefOf.LandedInPod, pawn);
					}
				}
			}
		}

		// gets the most likely map generator def, since we don't know which one was used to generate the current map
		public static MapGeneratorDef TryGetMostLikelyMapGenerator() {
			var allDefs = DefDatabase<MapGeneratorDef>.AllDefsListForReading;
			var highestSelectionWeight = -1f;
			MapGeneratorDef highestWeightDef = null;
			for (int i = 0; i < allDefs.Count; i++) {
				var def = allDefs[i];
				if (def.selectionWeight > highestSelectionWeight) {
					highestSelectionWeight = def.selectionWeight;
					highestWeightDef = def;
				}
			}
			return highestWeightDef;
		}

		// Genstep_Scatterer instances build up internal state during generation
		// if not reset, after enough rerolls, the map generator will fail to find spots to place geysers, items, resources, etc.
		public static void ResetScattererGensteps(MapGeneratorDef mapGenerator) {
			if (mapGenerator == null) return;
			foreach (var genStepDef in mapGenerator.GenSteps) {
				var genstepScatterer = genStepDef.genStep as GenStep_Scatterer;
				if (genstepScatterer != null) {
					ResetScattererGenstepInternalState(genstepScatterer);
				}
			}
		}

		// Genstep_ScatterThings is prone to generating things in the same spot on occasion.
		// If that happens we try to run it a few more times to try and get new positions.
		public static void TryGenerateGeysersWithNewLocations(Map map, GenStep_ScatterThings geyserGen) {
			const int MaxGeyserGenerationAttempts = 5;
			var collisionsDetected = true;
			var attemptsRemaining = MaxGeyserGenerationAttempts;
			while (attemptsRemaining > 0 && collisionsDetected) {
				var usedSpots = new HashSet<IntVec3>(GetAllGeyserPositionsOnMap(map));
				// destroy existing geysers
				Thing.allowDestroyNonDestroyable = true;
				map.listerThings.ThingsOfDef(ThingDefOf.SteamGeyser).ForEach(t => t.Destroy());
				Thing.allowDestroyNonDestroyable = false;
				// make new geysers
				geyserGen.Generate(map);
				// clean up internal state
				ResetScattererGenstepInternalState(geyserGen);
				// check if some geysers were generated in the same spots
				collisionsDetected = false;
				foreach (var geyserPos in GetAllGeyserPositionsOnMap(map)) {
					if (usedSpots.Contains(geyserPos)) {
						collisionsDetected = true;
					}
				}
				attemptsRemaining--;
			}
		}

		public static void ReduceMapResources(Map map, float consumePercent, float resourcesPercentBalance) {
			if (resourcesPercentBalance == 0) return;
			var rockDef = Find.World.NaturalRockTypesIn(map.Tile).FirstOrDefault();
			var mapResources = GetAllResourcesOnMap(map);

			var newResourceAmount = Mathf.Clamp(resourcesPercentBalance - consumePercent, 0, 100);
			var originalResAmount = Mathf.CeilToInt(mapResources.Count / (resourcesPercentBalance / 100));
			var percentageChange = resourcesPercentBalance - newResourceAmount;
			var resourceToll = Mathf.CeilToInt(Mathf.Abs(originalResAmount * (percentageChange / 100)));

			var toll = resourceToll;
			if (mapResources.Count > 0) {
				// eat random resources
				while (mapResources.Count > 0 && toll > 0) {
					var resIndex = Random.Range(0, mapResources.Count);
					var resThing = mapResources[resIndex];

					SneakilyDestroyResource(resThing);
					mapResources.RemoveAt(resIndex);
					if (rockDef != null && !RollForPirateStash(map, resThing.Position, originalResAmount)) {
						// put some rock in their place
						var rock = ThingMaker.MakeThing(rockDef);
						GenPlace.TryPlaceThing(rock, resThing.Position, map, ThingPlaceMode.Direct);
					}
					toll--;
				}
			}
			if (MapRerollController.Instance.SettingHandles.LogConsumedResources && Prefs.DevMode) {
				MapRerollController.Instance.Logger.Message("Ordered to consume " + consumePercent + "%, with current resources at " + resourcesPercentBalance + "%. Consuming " +
				                                            resourceToll + " resource spots, " + mapResources.Count + " left");
				if (toll > 0) MapRerollController.Instance.Logger.Message("Failed to consume " + toll + " resource spots.");
			}
			
		}

		public static List<Thing> GetAllResourcesOnMap(Map map) {
			return map.listerThings.AllThings.Where(t => t.def != null && t.def.building != null && t.def.building.mineableScatterCommonality > 0).ToList();
		}

		public static GenStep_ScatterThings TryGetGeyserGenstep() {
			var mapGenDef = TryGetMostLikelyMapGenerator();
			if (mapGenDef == null) return null;
			return (GenStep_ScatterThings)mapGenDef.GenSteps.Find(g => {
				var gen = g.genStep as GenStep_ScatterThings;
				return gen != null && gen.thingDef == ThingDefOf.SteamGeyser;
			}).genStep;
		}

		public static void SubtractResourcePercentage(Map map, float percent, MapRerollState rerollState) {
			ReduceMapResources(map, percent, rerollState.ResourcesPercentBalance);
			rerollState.ResourcesPercentBalance = Mathf.Clamp(rerollState.ResourcesPercentBalance - percent, 0, 100);
		}

		// receive the secret congrats letter if not already received 
		public static void TryReceiveSecretLetter(IntVec3 position, Map map) {
			var handle = MapRerollController.Instance.SettingHandles.SecretFound;
			if (handle.Value) return;
			Find.LetterStack.ReceiveLetter("MapReroll_secretLetter_title".Translate(), "MapReroll_secretLetter_text".Translate(), LetterDefOf.Good, new GlobalTargetInfo(position, map));
			MapRerollDefOf.RerollSecretFound.PlayOneShotOnCamera();
			handle.Value = true;
			HugsLibController.SettingsManager.SaveChanges();
		}

		public static void TryStopStartingPawnVomiting(Map map) {
			if (!MapRerollController.Instance.SettingHandles.NoVomiting) return;
			foreach (var pawn in GetAllColonistsOnMap(map)) {
				foreach (var hediff in pawn.health.hediffSet.hediffs) {
					if (hediff.def != HediffDefOf.CryptosleepSickness) continue;
					pawn.health.RemoveHediff(hediff);
					break;
				}
			}
		}

		public static void KillMapIntroDialog() {
			Find.WindowStack.TryRemove(typeof(Dialog_NodeTree), false);
		}

		private static bool RollForPirateStash(Map map, IntVec3 position, int originalMapResourceCount) {
			var stashDef = MapRerollDefOf.PirateStash.building as BuildingProperties_PirateStash;
			if (stashDef == null || originalMapResourceCount == 0 || stashDef.commonality == 0 || !LocationIsSuitableForStash(map, position)) return false;
			var mapSize = map.Size;
			// bias 0 for default map size, bias 1 for max map size
			var mapSizeBias = ((((mapSize.x * mapSize.z) / (float)MapRerollController.DefaultMapSize) - 1) / ((MapRerollController.LargestMapSize / (float)MapRerollController.DefaultMapSize) - 1)) * stashDef.mapSizeCommonalityBias;
			var rollSuccess = Rand.Range(0f, 1f) < 1 / (originalMapResourceCount / (stashDef.commonality + mapSizeBias));
			if (!rollSuccess) return false;
			var stash = ThingMaker.MakeThing(MapRerollDefOf.PirateStash);
			GenSpawn.Spawn(stash, position, map);
#if TEST_STASH
			MapRerollController.Instance.Logger.Trace("Placed stash at " + position);
#endif
			return true;
		}

		// check for double-thick walls to prevent players peeking through fog
		private static bool LocationIsSuitableForStash(Map map, IntVec3 position) {
			var grid = map.edificeGrid;
			if (!position.InBounds(map) || grid[map.cellIndices.CellToIndex(position)] != null) return false;
			for (int i = 0; i < 4; i++) {
				if (grid[map.cellIndices.CellToIndex(position + GenAdj.CardinalDirections[i])] == null) return false;
				if (grid[map.cellIndices.CellToIndex(position + GenAdj.CardinalDirections[i] * 2)] == null) return false;
			}
			return true;
		}

		/*
		 * destroying a resource outright causes too much overhead: fog, area reveal, pathing, roof updates, etc
		 * we just want to replace it. So, we just despawn it and do some cleanup.
		 * As of A13 despawning triggers all of the above. So, we do all the cleanup manually.
		 * The following is Thing.Despawn code with the unnecessary (for buildings, ar least) parts stripped out, plus key parts from Building.Despawn
		 * TODO: This approach may break with future releases (if thing despawning changes), so it's worth checking over.
		 */
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

		private static void ResetScattererGenstepInternalState(GenStep_Scatterer genstep) {
			var usedSpots = (List<IntVec3>)ReflectionCache.GenstepScatterer_UsedSpots.GetValue(genstep);
			if (usedSpots != null) {
				usedSpots.Clear();
			}
		}

		private static IEnumerable<IntVec3> GetAllGeyserPositionsOnMap(Map map) {
			return map.listerThings.ThingsOfDef(ThingDefOf.SteamGeyser).Select(t => t.Position);
		}

		private static void DespawnPawns(IEnumerable<Pawn> pawns) {
			foreach (var pawn in pawns) {
				if (pawn.Spawned) { // pawn might still be in pod
					pawn.DeSpawn();
				}
			}
		}

		private static void RemoveAllBondedPetRelations(IEnumerable<Pawn> pawns) {
			foreach (var pawn in pawns) {
				foreach (var relation in pawn.relations.DirectRelations.ToArray()) {
					if (relation.def == PawnRelationDefOf.Bond) {
						pawn.relations.RemoveDirectRelation(relation);
					}
				}
			}
		}

		public static class LoadingMessages {
			private const string LoadingMessageKey = "GeneratingMap";
			private const string CustomLoadingMessagePrefix = "MapReroll_loading";
			
			private static string stockLoadingMessage;
			private static int numAvailableLoadingMessages;

			public static void UpdateAvailableLoadingMessageCount() {
				numAvailableLoadingMessages = CountAvailableLoadingMessages();
			}

			public static void SetCustomLoadingMessage(bool customMessagesEnabled) {
				stockLoadingMessage = LoadingMessageKey.Translate();
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
				LanguageDatabase.activeLanguage.keyedReplacements[LoadingMessageKey] = message;
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