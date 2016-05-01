using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace MapReroll {
	public class MapRerollController {
		public enum MapRerollType {
			Map, Geyser
		}

		public static readonly string ModName = "Map Reroll";

		private static MapRerollController instance;
		public static MapRerollController Instance {
			get {
				return instance ?? (instance = new MapRerollController());
			}
		}

		// feel free to use these to detect reroll events 
		public event Action OnMapRerolled;
		public event Action OnGeysersRerolled;

		public MapRerollDef SettingsDef { get; private set; }

		public float ResourcePercentageRemaining { get; private set; }

		public bool LogConsumedResourceAmounts = false;
		public bool disableOnLoadedMaps = true;

		public bool ShowInterface {
			get { return SettingsDef != null && SettingsDef.enableInterface && (Find.Map!=null && !Find.ColonyInfo.ColonyHasName) && (!disableOnLoadedMaps || MapInitData.mapToLoad == null); }
		}

		private readonly FieldInfo thingPrivateStateField = typeof(Thing).GetField("thingStateInt", BindingFlags.Instance | BindingFlags.NonPublic);
		private readonly FieldInfo genstepScattererProtectedUsedSpots = typeof(Genstep_Scatterer).GetField("usedSpots", BindingFlags.Instance | BindingFlags.NonPublic);

		private bool mapRerollTriggered;
		private string originalWorldSeed;

		public void Notify_OnLevelLoaded() {
			SettingsDef = DefDatabase<MapRerollDef>.GetNamed("mapRerollSettings", false);
			if(SettingsDef==null) {
				Log.Error("[MapReroll] Settings Def was not loaded.");
				return;
			}
			// restore damaged MapInitData
			// it's essential we ADD the pawns to the list, rather than assign a new list here. It sidesteps an oversight in the early release version of Prepare Carefully (A13)
			foreach (var pawn in GetAllColonistsOnMap()) {
				MapInitData.colonists.Add(pawn);
			}
			// reset Genstep_Scatterer interal state
			ResetScattererGensteps();

			if(mapRerollTriggered) {
				ReduceMapResources(100-(ResourcePercentageRemaining), 100);
				SubtractResourcePercentage(SettingsDef.mapRerollCost);
				mapRerollTriggered = false;
				Find.World.info.seedString = originalWorldSeed;
				KillIntroDialog();
				if(OnMapRerolled!=null) OnMapRerolled();
			} else {
				ResourcePercentageRemaining = 100f;
				originalWorldSeed = Find.World.info.seedString;
			}
		}

		public void RerollMap() {
			mapRerollTriggered = true;
			Action newEventAction = delegate {
				var pawns = GetAllColonistsOnMap();
				foreach (var pawn in pawns) {
					if (pawn.IsColonist) {
						// colonist might still be in pod
						if (pawn.Spawned) {
							pawn.ClearMind();
							pawn.ClearReservations();
							pawn.DeSpawn();
						}
						// clear relation with bonded pet
						var bondedPet = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond);
						if(bondedPet!=null) {
							pawn.relations.RemoveDirectRelation(PawnRelationDefOf.Bond, bondedPet);
						}
					}
				}
				Find.World.info.seedString = Rand.Int.ToString();
				MapInitData.mapToLoad = null;
				Application.LoadLevel("Gameplay");
			};
			LongEventHandler.QueueLongEvent(newEventAction, GetLoadingMessage(), false, null);
		}
		
		public void RerollGeysers() {
			var geyserGen = TryGetGeyserGenstep();
			if (geyserGen != null) {
				TryGenerateGeysersWithNewLocations(geyserGen);
				SubtractResourcePercentage(SettingsDef.geyserRerollCost);
				if(OnGeysersRerolled!=null) OnGeysersRerolled();
			} else {
				Log.Error("Failed to find the Genstep for geysers. Check your map generator config.");
			}
		}

		public bool CanAffordOperation(MapRerollType type) {
			float cost = 0;
			switch (type) {
				case MapRerollType.Map: cost = SettingsDef.mapRerollCost; break;
				case MapRerollType.Geyser: cost = SettingsDef.geyserRerollCost; break;
			}
			return ResourcePercentageRemaining >= cost;
		}

		// get all colonists, including those still in drop pods
		private List<Pawn> GetAllColonistsOnMap() {
			return Find.MapPawns.PawnsInFaction(Faction.OfColony).Where(p => p.IsColonist).ToList();
		}

		// Genstep_Scatterer instances build up internal state during generation
		// if not reset, after enough rerolls, the map generator will fail to find spots to place geysers, items, resources, etc.
		private void ResetScattererGensteps() {
			var mapGenDef = DefDatabase<MapGeneratorDef>.AllDefs.FirstOrDefault();
			if (mapGenDef == null) return;
			foreach (var genstep in mapGenDef.genSteps) {
				var genstepScatterer = genstep as Genstep_Scatterer;
				if (genstepScatterer != null) {
					ResetScattererGenstepInternalState(genstepScatterer);		
				}
			}
		}

		private void ResetScattererGenstepInternalState(Genstep_Scatterer genstep) {
			// field is protected, use reflection
			var usedSpots = (HashSet<IntVec3>) genstepScattererProtectedUsedSpots.GetValue(genstep);
			if(usedSpots!=null) {
				usedSpots.Clear();
			}
		}

		// Genstep_ScatterThings is prone to generating things in the same spot on occasion.
		// If that happens we try to run it a few more times to try and get new positions.
		private void TryGenerateGeysersWithNewLocations(Genstep_ScatterThings geyserGen) {
			const int MaxGeyserGenerationAttempts = 10;
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
			var allResourceDefs = DefDatabase<ThingDef>.AllDefs.Where(def => def.building != null && def.building.mineableScatterCommonality > 0).ToArray();
			var rockDef = Find.World.NaturalRockTypesIn(Find.Map.WorldCoords).FirstOrDefault();
			var mapResources = Find.ListerThings.AllThings.Where(t => allResourceDefs.Contains(t.def)).ToList();

			var newResourceAmount = Mathf.Clamp(currentResourcesAtPercent - consumePercent, 0, 100);
			var originalResAmount = Mathf.Ceil(mapResources.Count / (currentResourcesAtPercent/100));
			var percentageChange = currentResourcesAtPercent - newResourceAmount;
			var resourceToll = (int)Mathf.Ceil(Mathf.Abs(originalResAmount * (percentageChange/100)));

			var toll = resourceToll;
			if (mapResources.Count > 0) {
				// eat random resources
				while (mapResources.Count > 0 && toll > 0) {
					var resIndex = UnityEngine.Random.Range(0, mapResources.Count);
					var resThing = mapResources[resIndex];

					SneakilyDestroyResource(resThing);
					mapResources.RemoveAt(resIndex);
					// put some rock in their place
					if (rockDef != null) {
						var rock = ThingMaker.MakeThing(rockDef);
						GenPlace.TryPlaceThing(rock, resThing.Position, ThingPlaceMode.Direct);
					}
					toll--;
				}
			}
			if (!LogConsumedResourceAmounts) return;
			Log.Message("[MapReroll] Ordered to consume " + consumePercent + "%, with current resources at " + currentResourcesAtPercent + "%. Consuming " + resourceToll + " resource spots, " + mapResources.Count + " left");
			if (toll > 0) Log.Message("[MapReroll] Failed to consume " + toll + " resource spots.");
		}

		private void SubtractResourcePercentage(float percent) {
			ReduceMapResources(percent, ResourcePercentageRemaining);
			ResourcePercentageRemaining = Mathf.Clamp(ResourcePercentageRemaining - percent, 0, 100);
		}

		// destroying a resource outright causes too much overhead: fog, area reveal, pathing, roof updates, etc
		// we just want to replace it. So, we just despawn it and do some cleanup.
		// As of A13 despawning triggers all of the above. So, we do all the cleanup manually. Dirty, but necessary.
		// The following is Thing.Despawn code with compromising parts stripped out, plus key parts from Building.Despawn
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
				CellRect cellRect = res.OccupiedRect();
				for (int i = cellRect.minZ; i <= cellRect.maxZ; i++) {
					for (int j = cellRect.minX; j <= cellRect.maxX; j++) {
						Find.Map.mapDrawer.MapMeshDirty(new IntVec3(j, 0, i), MapMeshFlag.Things);
					}
				}
			}
			if (res.def.drawerType != DrawerType.MapMeshOnly) {
				Find.DynamicDrawManager.DeRegisterDrawable(res);
			}
			thingPrivateStateField.SetValue(res, ThingState.Destroyed);
			Find.TickManager.DeRegisterAllTickabilityFor(res);
			Find.AttackTargetsCache.Notify_ThingDespawned(res);
			// building-specific cleanup 
			Find.ListerBuildings.Remove((Building) res);
			Find.DesignationManager.RemoveAllDesignationsOn(res);
		}

		private Genstep_ScatterThings TryGetGeyserGenstep() {
			var mapGenDef = DefDatabase<MapGeneratorDef>.AllDefs.FirstOrDefault();
			if (mapGenDef == null) return null;
			return (Genstep_ScatterThings)mapGenDef.genSteps.Find(g => {
				var gen = g as Genstep_ScatterThings;
				return gen != null && gen.thingDefs.Count == 1 && gen.thingDefs[0] == ThingDefOf.SteamGeyser;
			});
		}

		private string GetLoadingMessage() {
			return "MapReroll_defaultLoadingMsg".Translate()+"...";
		}

		private void KillIntroDialog(){
			Find.WindowStack.TryRemove(typeof(Dialog_NodeTree), false);
		}
	}
}