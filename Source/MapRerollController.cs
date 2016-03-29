using System;
using System.Linq;
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
			get { return SettingsDef != null && SettingsDef.enableInterface && !Find.ColonyInfo.ColonyHasName && (!disableOnLoadedMaps || MapInitData.mapToLoad == null); }
		}

		private bool mapRerollTriggered;

		public void Notify_OnLevelLoaded() {
			SettingsDef = DefDatabase<MapRerollDef>.GetNamed("mapRerollSettings", false);
			if(SettingsDef==null) {
				Log.Error("[MapReroll] Settings Def was not loaded.");
				return;
			}
			if(mapRerollTriggered) {
				ReduceMapResources(100-(ResourcePercentageRemaining), 100);
				SubtractResourcePercentage(SettingsDef.mapRerollCost);
				mapRerollTriggered = false;
				KillIntroDialog();
				if(OnMapRerolled!=null) OnMapRerolled();
			} else {
				ResourcePercentageRemaining = 100f;
			}
		}

		public void RerollMap() {
			mapRerollTriggered = true;
			Action newEventAction = delegate {
				var pawns = Find.ListerPawns.AllPawns.ToList();
				foreach (var pawn in pawns) {
					// preserve colonists for next map load
					if (pawn.IsColonist) {
						pawn.ClearMind();
						pawn.ClearReservations();
						pawn.DeSpawn();
					}
				}
				Find.World.info.seedString = Rand.Int.ToString();
				MapInitData.mapToLoad = null;
				Application.LoadLevel("Gameplay");
			};
			LongEventHandler.QueueLongEvent(newEventAction, GetLoadingMessage());
		}
		
		public void RerollGeysers() {
			var geyserGen = TryGetGeyserGenstep();
			if (geyserGen != null) {
				// trash existing geysers
				Thing.allowDestroyNonDestroyable = true;
				Find.ListerThings.ThingsOfDef(ThingDefOf.SteamGeyser).ForEach(t => t.Destroy());
				Thing.allowDestroyNonDestroyable = false;
				// poke some new ones
				geyserGen.Generate();
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

		private void ReduceMapResources(float consumePercent, float currentResourcesAtPercent) {
			if (currentResourcesAtPercent == 0) return;
			var allResourceDefs = DefDatabase<ThingDef>.AllDefs.Where(def => def.building != null && def.building.mineableScatterCommonality > 0).ToArray();
			var rockDef = Find.World.NaturalRockTypesIn(Find.Map.WorldCoords).FirstOrDefault();
			var mapResources = Find.ListerThings.AllThings.Where(t => allResourceDefs.Contains(t.def)).ToList();

			var newResourceAmount = Mathf.Clamp(currentResourcesAtPercent - consumePercent, 0, 100);
			var originalResAmount = Mathf.Ceil(mapResources.Count / (currentResourcesAtPercent/100));
			var percentageChange = currentResourcesAtPercent - newResourceAmount;
			var resourceToll = (int)Mathf.Ceil(Mathf.Abs(originalResAmount * (percentageChange/100)));

			if (mapResources.Count > 0) {
				var toll = resourceToll;
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
				if (LogConsumedResourceAmounts) Log.Message("[MapReroll] Ordered to consume " + consumePercent + "%, with current resources at " + currentResourcesAtPercent + "%. Eaten " + resourceToll + " resource spots, " + mapResources.Count + " left");
			}

		}

		private void SubtractResourcePercentage(float percent) {
			ReduceMapResources(percent, ResourcePercentageRemaining);
			ResourcePercentageRemaining = Mathf.Clamp(ResourcePercentageRemaining - percent, 0, 100);
		}

		// destroying a resource outright causes too much overhead: fog, area reveal, pathing, roof updates, etc
		// we just want to replace it. So, we just despawn it and do some cleanup.
		// Hopefully this won't cause any issues. Let me know, if you believe otherwise :)
		private void SneakilyDestroyResource(Thing res) {
			res.DeSpawn();
			Find.ListerBuildings.Remove((Building) res);
			Find.DesignationManager.RemoveAllDesignationsOn(res);
			Find.DesignationManager.Notify_BuildingDestroyed(res);
		}

		private Genstep_ScatterThings TryGetGeyserGenstep() {
			var mapGenDef = DefDatabase<MapGeneratorDef>.AllDefs.FirstOrDefault();
			if (mapGenDef == null) return null;
			var genstep = (Genstep_ScatterThings)mapGenDef.genSteps.Find(g => {
				var gen = g as Genstep_ScatterThings;
				return gen != null && gen.thingDefs.Count == 1 && gen.thingDefs[0] == ThingDefOf.SteamGeyser;
			});
			// make a shallow copy, since gensteps have internal state
			var newgen = new Genstep_ScatterThings {
				thingDefs = genstep.thingDefs,
				minSpacing = genstep.minSpacing,
				extraNoBuildEdgeDist = genstep.extraNoBuildEdgeDist,
				countPer10kCellsRange = genstep.countPer10kCellsRange,
				clearSpaceSize = genstep.clearSpaceSize,
				neededSurfaceType = genstep.neededSurfaceType,
				validators = genstep.validators
			};
			return newgen;
		}

		private string GetLoadingMessage() {
			if(SettingsDef.useSillyLoadingMessages) {
				var messageIndex = UnityEngine.Random.Range(0, SettingsDef.numLoadingMessages - 1);
				var messageKey = "MapReroll_loading" + messageIndex;
				if (messageKey.CanTranslate()) {
					return messageKey.Translate()+"...";
				}
			}
			return "MapReroll_defaultLoadingMsg".Translate()+"...";
		}

		private void KillIntroDialog() {
			Find.WindowStack.TryRemove(typeof(Dialog_NodeTree), false);
		}
	}
}